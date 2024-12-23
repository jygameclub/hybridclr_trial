using HybridCLR;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// LoadDll 类用于在运行时下载并加载 DLL 文件，实现热更新功能。
/// 使用了 HybridCLR 来支持在 IL2CPP 构建的项目中动态加载和执行 C# 代码。
/// </summary>
public class LoadDll : MonoBehaviour
{
    /// <summary>
    /// Unity 的 Start 方法，在脚本启动时调用。
    /// 启动协程以下载必要的资源并在下载完成后启动游戏。
    /// </summary>
    void Start()
    {
        StartCoroutine(DownLoadAssets(this.StartGame));
    }

    #region 下载资源

    // 存储下载的资源数据，键为资源名称，值为对应的字节数组
    private static Dictionary<string, byte[]> s_assetDatas = new Dictionary<string, byte[]>();

    /// <summary>
    /// 从 StreamingAssets 文件夹中读取指定 DLL 的字节数据。
    /// </summary>
    /// <param name="dllName">DLL 文件名</param>
    /// <returns>对应 DLL 的字节数组</returns>
    public static byte[] ReadBytesFromStreamingAssets(string dllName)
    {
        return s_assetDatas[dllName];
    }

    /// <summary>
    /// 构建用于网络请求的资源路径。
    /// </summary>
    /// <param name="asset">资源名称</param>
    /// <returns>完整的网络请求路径</returns>
    private string GetWebRequestPath(string asset)
    {
        var path = $"{Application.streamingAssetsPath}/{asset}";
        // 如果路径中不包含协议头，则添加 "file://" 前缀
        if (!path.Contains("://"))
        {
            path = "file://" + path;
        }
        return path;
    }

    /// <summary>
    /// 定义 AOT（Ahead Of Time）元数据程序集的文件名列表。
    /// 这些程序集需要在运行时加载元数据，以支持 HybridCLR 的 AOT 特性。
    /// </summary>
    private static List<string> AOTMetaAssemblyFiles { get; } = new List<string>()
    {
        "mscorlib.dll.bytes",
        "System.dll.bytes",
        "System.Core.dll.bytes",
    };

    /// <summary>
    /// 协程，用于下载所有必要的资源文件。
    /// 下载完成后调用指定的回调函数。
    /// </summary>
    /// <param name="onDownloadComplete">下载完成后的回调函数</param>
    /// <returns>IEnumerator 用于协程执行</returns>
    IEnumerator DownLoadAssets(Action onDownloadComplete)
    {
        // 定义需要下载的资源列表，包括预制体和热更新 DLL，以及 AOT 元数据程序集
        var assets = new List<string>
        {
            "prefabs",
            "HotUpdate.dll.bytes",
        }.Concat(AOTMetaAssemblyFiles);

        // 遍历每个资源并进行下载
        foreach (var asset in assets)
        {
            string dllPath = GetWebRequestPath(asset);
            Debug.Log($"开始下载资源: {dllPath}");
            UnityWebRequest www = UnityWebRequest.Get(dllPath);
            yield return www.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
            // Unity 2020.1 及以上版本的错误处理
            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.Log(www.error);
            }
#else
            // Unity 2020.1 以下版本的错误处理
            if (www.isHttpError || www.isNetworkError)
            {
                Debug.Log(www.error);
            }
#endif
            else
            {
                // 成功下载后，将字节数据存储到字典中
                byte[] assetData = www.downloadHandler.data;
                Debug.Log($"下载完成: {asset} 大小: {assetData.Length} 字节");
                s_assetDatas[asset] = assetData;
            }
        }

        // 下载完成后，调用回调函数开始游戏
        onDownloadComplete();
    }

    #endregion

    // 存储热更新程序集的 Assembly 对象
    private static Assembly _hotUpdateAss;

    /// <summary>
    /// 加载 AOT 程序集的元数据。
    /// 这段代码可以放在 AOT 程序集或热更新程序集内。
    /// 加载后，如果 AOT 泛型函数对应的本地实现不存在，则自动切换为解释执行模式。
    /// </summary>
    private static void LoadMetadataForAOTAssemblies()
    {
        /// 注意：
        /// 这里只补充 AOT DLL 的元数据，不补充热更新 DLL 的元数据。
        /// 热更新 DLL 不缺少元数据，因此不需要补充。如果尝试为热更新 DLL 补充元数据，会返回错误。
        ///
        HomologousImageMode mode = HomologousImageMode.SuperSet;
        foreach (var aotDllName in AOTMetaAssemblyFiles)
        {
            // 从已下载的资源中读取 AOT DLL 的字节数据
            byte[] dllBytes = ReadBytesFromStreamingAssets(aotDllName);
            // 使用 HybridCLR 的 RuntimeApi 加载 AOT DLL 的元数据
            // 这会自动为其 hook，如果 AOT 泛型函数的本地实现不存在，则使用解释器版本
            LoadImageErrorCode err = RuntimeApi.LoadMetadataForAOTAssembly(dllBytes, mode);
            Debug.Log($"加载 AOT 元数据: {aotDllName}, 模式: {mode}, 结果: {err}");
        }
    }

    /// <summary>
    /// 游戏启动逻辑，加载热更新程序集并调用入口方法。
    /// 还会实例化资源并启动自动退出协程。
    /// </summary>
    void StartGame()
    {
        // 加载 AOT 程序集的元数据
        LoadMetadataForAOTAssemblies();

#if !UNITY_EDITOR
        // 在非编辑器环境下，从已下载的热更新 DLL 字节数据加载 Assembly
        _hotUpdateAss = Assembly.Load(ReadBytesFromStreamingAssets("HotUpdate.dll.bytes"));
#else
        // 在编辑器环境下，直接从当前 AppDomain 中获取已加载的热更新程序集
        _hotUpdateAss = System.AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == "HotUpdate");
#endif

        // 获取热更新程序集中的入口类型 "Entry"
        Type entryType = _hotUpdateAss.GetType("Entry");
        // 反射调用 "Entry" 类型中的静态方法 "Start"
        entryType.GetMethod("Start").Invoke(null, null);

        // 实例化预制体中的组件
        Run_InstantiateComponentByAsset();

        // 启动一个协程，延迟一段时间后自动退出应用
        StartCoroutine(DelayAndQuit());
    }

    /// <summary>
    /// 协程，用于延迟一段时间后自动退出应用。
    /// 在 Windows 平台下，会在当前目录下生成一个 "run.log" 文件。
    /// </summary>
    /// <returns>IEnumerator 用于协程执行</returns>
    IEnumerator DelayAndQuit()
    {
#if UNITY_STANDALONE_WIN
        // 在 Windows 平台下，写入 "run.log" 文件，内容为 "ok"
        File.WriteAllText(Directory.GetCurrentDirectory() + "/run.log", "ok", System.Text.Encoding.UTF8);
#endif
        // 从 10 秒倒计时，每秒输出一次日志
        for (int i = 10; i >= 1; i--)
        {
            UnityEngine.Debug.Log($"将于 {i} 秒后自动退出");
            yield return new WaitForSeconds(1f);
        }
        // 退出应用
        Application.Quit();
    }

    /// <summary>
    /// 实例化 AssetBundle 中的资源，并还原资源上的热更新脚本。
    /// </summary>
    private static void Run_InstantiateComponentByAsset()
    {
        // 从已下载的 "prefabs" 资源中加载 AssetBundle
        AssetBundle ab = AssetBundle.LoadFromMemory(LoadDll.ReadBytesFromStreamingAssets("prefabs"));
        // 从 AssetBundle 中加载名为 "Cube" 的预制体
        GameObject cube = ab.LoadAsset<GameObject>("Cube");
        // 实例化加载的预制体
        GameObject.Instantiate(cube);
    }
}
