using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System;
using UnityEngine.Events;
using Cysharp.Threading.Tasks;
using DG.Tweening;

#if UNITY_EDITOR
using UnityEditor;
[CustomEditor(typeof(Scene_Manager))]
public class CustomEdit : Editor
{
    string symbol = "UNITASK_DOTWEEN_SUPPORT";
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (!Application.isPlaying)
        {
            GUILayout.Space(20);
            symbol = GUILayout.TextField(symbol, GUILayout.Height(20));
            if (GUILayout.Button("AddScriptingDefine", GUILayout.Height(20)))
            {
                if (symbol == "") return;
                string currentSymbol = PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
                if (currentSymbol.IndexOf(symbol) < 0)
                {
                    currentSymbol = currentSymbol + ";" + symbol;
                    PlayerSettings.SetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup,
                        currentSymbol);
                }
                AssetDatabase.Refresh();
            }
        }
    }
}
#endif
public class Scene_Manager : Singleton.SingletonMonoBehaviour<Scene_Manager>
{
    public Image backimg;
    public static event UnityAction<string> activeSceneChanging;
    bool sceneChanging;

    protected override void Awake()
    {
        base.Awake();
        SceneManager.activeSceneChanged += ActiveSceneChanged;
        DontDestroyOnLoad(this.gameObject);
    }
    private void ActiveSceneChanged(Scene thisScene, Scene nextScene)
    {
        sceneChanging = false;
#if UNITY_EDITOR
        Debug.Log(thisScene.name + "=>" + nextScene.name);
#endif
    }

#if UNITY_EDITOR
    /// <summary>
    /// シーンの存在をテストする。 
    /// </summary>
    public bool LoadLevelTest(string scene)
    {
        bool update = false;
        System.Collections.Generic.List<UnityEditor.EditorBuildSettingsScene> buildScenes = new System.Collections.Generic.List<UnityEditor.EditorBuildSettingsScene>(UnityEditor.EditorBuildSettings.scenes);
        var guids = UnityEditor.AssetDatabase.FindAssets("t:Scene");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
            UnityEditor.SceneAsset sceneAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(path);
            UnityEditor.EditorBuildSettingsScene buildScene = buildScenes.Find((editorBuildScene) =>
            {
                return editorBuildScene.path == path;
            });

            if (sceneAsset.name == scene || sceneAsset.name == SceneManager.GetActiveScene().name)
            {
                if (buildScene == null)
                {
                    buildScenes.Add(new UnityEditor.EditorBuildSettingsScene(path, true));
                    UnityEditor.EditorBuildSettings.scenes = buildScenes.ToArray();
                    Debug.Log("AddToBuild:" + sceneAsset.name);
                }
                update = true;
            }
        }
        return update;
    }
#endif
    /// <summary>
    /// 画面遷移
    /// Scene_Manager.activeSceneChanging += (s) => { BGM_SE_Manager.Instance.bgm_se_setting = _bgm_se_setting; };
    /// Scene_Manager.LoadLevel("Scene", false);
    /// </summary>
    /// <param name='scene'>シーン名</param>
    public static void LoadLevel(string scene, bool Additive)
    {
        Instance.LoadLevel(scene, 0.5f, true, Additive);
    }
    public void LoadLevel(string scene, float Loadspeed, bool ResourceUnload, bool Additive)
    {
#if UNITY_EDITOR
        if (!LoadLevelTest(scene)) return;
#endif
        if (!sceneChanging)
            TransSceneAsync(scene, Loadspeed, ResourceUnload, Additive ? LoadSceneMode.Additive : LoadSceneMode.Single).Forget();
    }

    private bool CreateImage()
    {
        if (backimg != null) 
            return true;

        GameObject canvas = GameObject.Find("Canvas");
        if (canvas != null)
        {
            GameObject Image = new GameObject("Image");
            Image.transform.SetParent(canvas.transform);
            backimg = Image.AddComponent<Image>();
            backimg.rectTransform.sizeDelta = new Vector2(Screen.width, Screen.height);
            backimg.rectTransform.anchoredPosition = new Vector2(0.0f, 0.0f);
            backimg.color = new Color(0f, 0f, 0f, 0f);
        }
        return backimg != null;
    }

    /// <summary>
    /// シーン遷移
    /// </summary>
    /// <param name='scene'>シーン名</param>
    /// <param name='interval'>暗転にかかる時間(秒)</param>
    private async UniTask TransSceneAsync(string scene, float Loadspeed, bool ResourceUnload, LoadSceneMode loadSceneMode)
    {
        // シーン遷移中
        sceneChanging = true;
        AsyncOperation async = SceneManager.LoadSceneAsync(scene, loadSceneMode);
        async.allowSceneActivation = false;
        
        if (CreateImage())
        {
            try
            {
                //だんだん暗く
                var fade = this.backimg.DOFade(1.0f, 1.0f - Loadspeed);

                if (activeSceneChanging != null)
                    activeSceneChanging(scene);

                //メモリ解放
                if (ResourceUnload)
                {
                    GC.Collect();
                    _ = Resources.UnloadUnusedAssets();
                    GC.Collect();
                }
                //ギリギリまで待つ(isDoneだとtrueにならない)
                await UniTask.WaitUntil(() => async.progress >= 0.9f);
                //edit / project settings / script compile / Add "UNITASK_DOTWEEN_SUPPORT"
                await fade;
            }
            catch (Exception ex)
            {
                CancelLoadSceneAsync(async, SceneManager.GetSceneByName(scene)).Forget();
                throw ex;
            }
        }
        //シーン遷移する
        async.allowSceneActivation = true;
    }
    static async UniTask CancelLoadSceneAsync(AsyncOperation loadOperation, Scene loadingScene)
    {
        var unloadOperation = SceneManager.UnloadSceneAsync(loadingScene);
        if (unloadOperation == null)
        {
            loadOperation.completed += _ =>
            {
                foreach (var go in loadingScene.GetRootGameObjects())
                {
                    go.SetActive(false);
                }
                unloadOperation = SceneManager.UnloadSceneAsync(loadingScene);
            };
            loadOperation.allowSceneActivation = true;
            await loadOperation;
        }
        await unloadOperation;
    }
}
