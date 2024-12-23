using UnityEngine;

/// <summary>
/// ConsoleToScreen 类用于将 Unity 的控制台日志显示在游戏画面上，并支持滚动查看。
/// </summary>
public class ConsoleToScreen : MonoBehaviour
{
    // 定义日志显示的最大行数
    const int maxLines = 50;
    // 定义每行显示的最大字符长度，超过则换行
    const int maxLineLength = 120;

    // 存储所有日志的字符串
    private string _logStr = "";
    // 记录滚动视图的位置
    private Vector2 _scrollPosition;
    // 日志文本的字体大小，可以在 Inspector 中调整
    public int fontSize = 15;

    /// <summary>
    /// 当脚本启用时，注册日志回调函数。
    /// </summary>
    private void OnEnable()
    {
        // 订阅 Unity 的日志消息接收事件
        Application.logMessageReceived += HandleLog;
    }

    /// <summary>
    /// 当脚本禁用时，注销日志回调函数。
    /// </summary>
    private void OnDisable()
    {
        // 取消订阅 Unity 的日志消息接收事件
        Application.logMessageReceived -= HandleLog;
    }

    /// <summary>
    /// 处理接收到的日志信息，将其格式化后添加到日志字符串中。
    /// </summary>
    /// <param name="logString">日志信息</param>
    /// <param name="stackTrace">堆栈跟踪信息</param>
    /// <param name="type">日志类型（如错误、警告、信息）</param>
    private void HandleLog(string logString, string stackTrace, LogType type)
    {
        // 按换行符分割日志字符串，处理多行日志
        var lines = logString.Split('\n');
        foreach (var line in lines)
        {
            // 如果当前行的长度不超过最大行长度，直接添加并换行
            if (line.Length <= maxLineLength)
            {
                _logStr += line + "\n";
            }
            else
            {
                // 如果当前行长度超过最大行长度，则进行多行切割
                var startIndex = 0;
                while (startIndex < line.Length)
                {
                    // 计算当前切割的长度，不超过最大行长度
                    var length = Mathf.Min(maxLineLength, line.Length - startIndex);
                    // 将切割后的子字符串添加到日志字符串中，并换行
                    _logStr += line.Substring(startIndex, length) + "\n";
                    // 更新下一个切割的起始索引
                    startIndex += maxLineLength;
                }
            }
        }

        // 将日志字符串按换行符分割成行数组
        var logLines = _logStr.Split('\n');
        // 如果日志行数超过最大行数，移除最早的日志行
        if (logLines.Length > maxLines)
        {
            // 计算需要移除的行数
            var linesToRemove = logLines.Length - maxLines;
            // 找到第一个换行符的位置
            var firstNewLineIndex = _logStr.IndexOf('\n');
            // 从日志字符串中移除最早的日志行
            _logStr = _logStr.Remove(0, firstNewLineIndex + 1);
        }
    }

    /// <summary>
    /// 每帧更新，确保滚动视图自动滚动到底部。
    /// </summary>
    private void Update()
    {
        // 将滚动位置的 y 值设置为无穷大，确保总是滚动到底部
        _scrollPosition.y = Mathf.Infinity;
    }

    /// <summary>
    /// 在 GUI 上绘制日志信息的界面。
    /// </summary>
    private void OnGUI()
    {
        // 定义一个区域，稍微留出边距（10像素）
        GUILayout.BeginArea(new Rect(10f, 10f, Screen.width - 20f, Screen.height - 20f));
        // 开始一个滚动视图，并记录当前的滚动位置
        _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUIStyle.none, GUIStyle.none);

        // 创建一个新的 GUIStyle，用于设置日志文本的样式
        GUIStyle style = new GUIStyle(GUI.skin.label);
        // 设置字体大小，确保字体大小至少为指定的值
        style.fontSize = fontSize;

        // 在滚动视图中绘制日志字符串
        GUILayout.Label(_logStr, style);

        // 结束滚动视图
        GUILayout.EndScrollView();
        // 结束定义的区域
        GUILayout.EndArea();
    }
}
