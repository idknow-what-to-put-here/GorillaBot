<<<<<<< HEAD
﻿using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace WalkSimulator
{
    public static class Logging
    {
        private static ManualLogSource logger;
        public static int DebuggerLines = 20;
        private static readonly List<string> fullLogs = new List<string>();
        private static readonly object logLock = new object();

        public static event Action<string> OnLogMessage;

        public static void Init()
        {
            logger = Logger.CreateLogSource("WalkSimulator");
        }

        private static string GetContext()
        {
            try
            {
                StackFrame frame = new StackTrace(2, true).GetFrame(0);
                MethodBase method = frame.GetMethod();
                string className = method.ReflectedType?.Name ?? "UnknownClass";
                string methodName = method.Name;
                string fileName = frame.GetFileName() ?? "UnknownFile";
                int lineNumber = frame.GetFileLineNumber();

                return $"{className}.{methodName} [{fileName}:{lineNumber}]";
            }
            catch
            {
                return "UnknownContext";
            }
        }

        private static void AddLogEntry(string level, object[] content)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string context = GetContext();
            string message = string.Join(" ", content);
            string threadId = System.Threading.Thread.CurrentThread.ManagedThreadId.ToString();

            string logEntry = $"[{timestamp}] [{level}] [Thread:{threadId}] {context} - {message}";

            lock (logLock)
            {
                fullLogs.Add(logEntry);
                while (fullLogs.Count > 10000)
                {
                    fullLogs.RemoveAt(0);
                }
            }

            OnLogMessage?.Invoke(logEntry);
        }

        public static void Exception(Exception e)
        {
            string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [ERROR] [Thread:{System.Threading.Thread.CurrentThread.ManagedThreadId}] {GetContext()} - {e}\nStack Trace: {e.StackTrace}";
            logger.LogError(logEntry);
            AddLogEntry("ERROR", new object[] { e.ToString(), "\nStack Trace:", e.StackTrace });
        }

        public static void Fatal(params object[] content)
        {
            logger.LogFatal(string.Join(" ", content));
            AddLogEntry("FATAL", content);
        }

        public static void Warning(params object[] content)
        {
            logger.LogWarning(string.Join(" ", content));
            AddLogEntry("WARN", content);
        }

        public static void Info(params object[] content)
        {
            logger.LogInfo(string.Join(" ", content));
            AddLogEntry("INFO", content);
        }

        public static void Debug(params object[] content)
        {
            logger.LogDebug(string.Join(" ", content));
            AddLogEntry("DEBUG", content);
        }

        public static void Debugger(params object[] content)
        {
            Debug(content);
        }

        public static string GetFullLogText()
        {
            lock (logLock)
            {
                return string.Join("\n", fullLogs);
            }
        }

        public static void ClearLogs()
        {
            lock (logLock)
            {
                fullLogs.Clear();
            }
        }
    }
}
=======
﻿using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace WalkSimulator
{
    public static class Logging
    {
        private static ManualLogSource logger;
        public static int DebuggerLines = 20;
        private static readonly List<string> fullLogs = new List<string>();
        private static readonly object logLock = new object();

        public static event Action<string> OnLogMessage;

        public static void Init()
        {
            logger = Logger.CreateLogSource("WalkSimulator");
        }

        private static string GetContext()
        {
            try
            {
                StackFrame frame = new StackTrace(2, true).GetFrame(0);
                MethodBase method = frame.GetMethod();
                string className = method.ReflectedType?.Name ?? "UnknownClass";
                string methodName = method.Name;
                string fileName = frame.GetFileName() ?? "UnknownFile";
                int lineNumber = frame.GetFileLineNumber();

                return $"{className}.{methodName} [{fileName}:{lineNumber}]";
            }
            catch
            {
                return "UnknownContext";
            }
        }

        private static void AddLogEntry(string level, object[] content)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string context = GetContext();
            string message = string.Join(" ", content);
            string threadId = System.Threading.Thread.CurrentThread.ManagedThreadId.ToString();

            string logEntry = $"[{timestamp}] [{level}] [Thread:{threadId}] {context} - {message}";

            lock (logLock)
            {
                fullLogs.Add(logEntry);
                while (fullLogs.Count > 10000)
                {
                    fullLogs.RemoveAt(0);
                }
            }

            OnLogMessage?.Invoke(logEntry);
        }

        public static void Exception(Exception e)
        {
            string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [ERROR] [Thread:{System.Threading.Thread.CurrentThread.ManagedThreadId}] {GetContext()} - {e}\nStack Trace: {e.StackTrace}";
            logger.LogError(logEntry);
            AddLogEntry("ERROR", new object[] { e.ToString(), "\nStack Trace:", e.StackTrace });
        }

        public static void Fatal(params object[] content)
        {
            logger.LogFatal(string.Join(" ", content));
            AddLogEntry("FATAL", content);
        }

        public static void Warning(params object[] content)
        {
            logger.LogWarning(string.Join(" ", content));
            AddLogEntry("WARN", content);
        }

        public static void Info(params object[] content)
        {
            logger.LogInfo(string.Join(" ", content));
            AddLogEntry("INFO", content);
        }

        public static void Debug(params object[] content)
        {
            logger.LogDebug(string.Join(" ", content));
            AddLogEntry("DEBUG", content);
        }

        public static void Debugger(params object[] content)
        {
            Debug(content);
        }

        public static string GetFullLogText()
        {
            lock (logLock)
            {
                return string.Join("\n", fullLogs);
            }
        }

        public static void ClearLogs()
        {
            lock (logLock)
            {
                fullLogs.Clear();
            }
        }
    }
}
>>>>>>> 31ec9aed7aef5857e654107e2b42ea026d51c0de
