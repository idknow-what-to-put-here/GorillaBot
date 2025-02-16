using BepInEx.Logging;
using System;
using System.Diagnostics;
using System.Reflection;

namespace WalkSimulator
{
    public static class Logging
    {
        private static ManualLogSource logger;
        public static int DebuggerLines = 20;

        public static void Init()
        {
            logger = Logger.CreateLogSource("WalkSimulator");
        }

        private static string GetContext()
        {
            StackFrame frame = new StackTrace().GetFrame(1);
            MethodBase method = frame.GetMethod();
            return $"({method.ReflectedType.Name}.{method.Name}())";
        }

        public static void Exception(Exception e)
        {
            logger.LogWarning($"{GetContext()} {e.Message} {e.StackTrace}");
        }

        public static void Fatal(params object[] content)
        {
            logger.LogFatal($"{GetContext()} {string.Join(" ", content)}");
        }

        public static void Warning(params object[] content)
        {
            logger.LogWarning($"{GetContext()} {string.Join(" ", content)}");
        }

        public static void Info(params object[] content)
        {
            logger.LogInfo($"{GetContext()} {string.Join(" ", content)}");
        }

        public static void Debug(params object[] content)
        {
            logger.LogDebug($"{GetContext()} {string.Join("  ", content)}");
        }

        public static void Debugger(params object[] content)
        {
            Debug(content);
        }

        public static string PrependTextToLog(string log, string text)
        {
            log = text + "\n" + log;
            string[] lines = log.Split('\n');
            if (lines.Length > DebuggerLines)
            {
                log = string.Join("\n", lines, 0, DebuggerLines);
            }
            return log;
        }
    }
}
