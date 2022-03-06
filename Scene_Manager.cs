using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;

#if UNITY_EDITOR
using UnityEditor;
[CustomEditor(typeof(Scene_Manager))]
public class Scene_ManagerCustomEdit : Editor
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
    public bool sceneChanging { get; private set; }

    protected override void Awake()
    {
        base.Awake();
        DontDestroyOnLoad(this.gameObject);
    }

#if UNITY_EDITOR
    /// <summary>
    /// シーンの存在をテストする。 
    /// </summary>
    public bool LoadLevelTest(string scene)
    {
        bool update = false;
        System.Collections.Generic.List<UnityEditor.EditorBuildSettingsScene> buildScenes = 
            new System.Collections.Generic.List<UnityEditor.EditorBuildSettingsScene>(UnityEditor.EditorBuildSettings.scenes);
        var guids = UnityEditor.AssetDatabase.FindAssets("t:Scene");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
            UnityEditor.SceneAsset sceneAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(path);
            UnityEditor.EditorBuildSettingsScene buildScene = buildScenes.Find((editorBuildScene) => editorBuildScene.path == path);
            if (sceneAsset.name == scene || sceneAsset.name == SceneManager.GetActiveScene().name)
            {
                if (buildScene == null)
                {
                    buildScenes.Add(new UnityEditor.EditorBuildSettingsScene(path, true));
                    Debug.Log("AddToBuild:" + sceneAsset.name);
                }

                if (sceneAsset.name == scene)
                    update = true;
            }
        }
        return update;
    }
#endif
    /// <summary>
    /// 画面遷移
    /// Scene_Manager.LoadLevel("Scene", (_async) => { 
    /// _async.completed += (_async) => { }; 
    /// });
    /// </summary>
    /// <param string='scene'>シーン名</param>
    /// <param Action='activeSceneChanging'>シーン切り替え中</param>
    public static void LoadLevel(string scene, in Action<AsyncOperation> activeSceneChanging)
    {
        Instance.LoadLevel(scene, 0.8f, true, false, activeSceneChanging);
    }
    public void LoadLevel(string scene, float Loadspeed, bool ResourceUnload, bool Additive, in Action<AsyncOperation> activeSceneChanging)
    {
        if (sceneChanging) return;
#if UNITY_EDITOR
        if (!LoadLevelTest(scene)) return;
#endif
        TransSceneAsync(scene, Loadspeed, ResourceUnload, Additive ? LoadSceneMode.Additive : LoadSceneMode.Single, activeSceneChanging).Forget();
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
    private async UniTask TransSceneAsync(string scene, float Loadspeed, bool ResourceUnload, LoadSceneMode loadSceneMode, Action<AsyncOperation> activeSceneChanging)
    {
        sceneChanging = true; // シーン遷移中
        AsyncOperation async = SceneManager.LoadSceneAsync(scene, loadSceneMode);
        async.allowSceneActivation = false;
        async.completed += _ => sceneChanging = false;

        if (CreateImage())
        {
            try
            {
                var fade = this.backimg.DOFade(1.0f, 1.0f - Loadspeed); //だんだん暗く

                if (activeSceneChanging != null)
                    activeSceneChanging(async);

                if (ResourceUnload) //メモリ解放
                {
                    GC.Collect();
                    _ = Resources.UnloadUnusedAssets();
                    GC.Collect();
                }

                await fade;
            }
            catch
            {
                await CancelLoadSceneAsync(async, SceneManager.GetSceneByName(scene));
            }
        }
        async.allowSceneActivation = true; //シーン遷移する
        await async;
    }
    private async UniTask CancelLoadSceneAsync(AsyncOperation loadOperation, Scene loadingScene)
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
                sceneChanging = false;
            };
            loadOperation.allowSceneActivation = true;
            await loadOperation;
        }
        await unloadOperation;
    }
}