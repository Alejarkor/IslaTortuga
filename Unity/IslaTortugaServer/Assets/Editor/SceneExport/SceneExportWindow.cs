using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IslaTortuga.Unity.SceneExport.Editor
{
    internal sealed class SceneExportWindow : EditorWindow
    {
        private const string ContentPackVersionKey = "IslaTortuga.SceneExport.ContentPackVersion";
        private Vector2 scrollPosition;
        private string contentPackVersion = "v001";
        private SceneExportResult lastResult;

        [MenuItem("Isla Tortuga/Scene Export/Export Active Scene")]
        private static void ExportActiveSceneMenu()
        {
            var window = GetWindow<SceneExportWindow>("Scene Exporter");
            window.Show();
            window.ExportActiveScene();
        }

        [MenuItem("Isla Tortuga/Scene Export/Validate Active Scene")]
        private static void ValidateActiveSceneMenu()
        {
            var window = GetWindow<SceneExportWindow>("Scene Exporter");
            window.Show();
            window.ValidateActiveScene();
        }

        [MenuItem("Window/Isla Tortuga/Scene Exporter")]
        private static void OpenWindow()
        {
            var window = GetWindow<SceneExportWindow>("Scene Exporter");
            window.Show();
        }

        private void OnEnable()
        {
            contentPackVersion = EditorPrefs.GetString(ContentPackVersionKey, "v001");
        }

        private void OnGUI()
        {
            DrawHeader();
            EditorGUILayout.Space();
            DrawSettings();
            EditorGUILayout.Space();
            DrawActions();
            EditorGUILayout.Space();
            DrawLastResult();
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("Unity Scene Export", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Exporta la escena activa al content-pack siguiendo la plantilla definida para Babylon y el server.",
                MessageType.Info);
        }

        private void DrawSettings()
        {
            EditorGUI.BeginChangeCheck();
            contentPackVersion = EditorGUILayout.TextField("Content Pack Version", contentPackVersion);
            if (EditorGUI.EndChangeCheck())
            {
                contentPackVersion = string.IsNullOrWhiteSpace(contentPackVersion) ? "v001" : contentPackVersion.Trim();
                EditorPrefs.SetString(ContentPackVersionKey, contentPackVersion);
            }
        }

        private void DrawActions()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Validar", GUILayout.Height(28f)))
                {
                    ValidateActiveScene();
                }

                if (GUILayout.Button("Exportar Escena Activa", GUILayout.Height(28f)))
                {
                    ExportActiveScene();
                }
            }
        }

        private void DrawLastResult()
        {
            if (lastResult == null)
            {
                EditorGUILayout.HelpBox("Todavia no se ha ejecutado ninguna validacion o exportacion.", MessageType.None);
                return;
            }

            var issueCount = lastResult.Issues.Count;
            var errorCount = lastResult.Issues.Count(issue => issue.Severity == SceneExportIssueSeverity.Error);
            var warningCount = lastResult.Issues.Count(issue => issue.Severity == SceneExportIssueSeverity.Warning);
            var infoCount = lastResult.Issues.Count(issue => issue.Severity == SceneExportIssueSeverity.Info);

            EditorGUILayout.LabelField("Ultimo Resultado", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Scene Id", string.IsNullOrWhiteSpace(lastResult.SceneId) ? "-" : lastResult.SceneId);
            EditorGUILayout.LabelField("Estado", lastResult.Success ? "OK" : "Con problemas");
            EditorGUILayout.LabelField("Issues", issueCount.ToString());
            EditorGUILayout.LabelField("Errores / Warnings / Info", errorCount + " / " + warningCount + " / " + infoCount);

            if (!string.IsNullOrWhiteSpace(lastResult.SceneDataPath))
            {
                EditorGUILayout.SelectableLabel(lastResult.SceneDataPath, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            for (var index = 0; index < lastResult.Issues.Count; index++)
            {
                DrawIssue(lastResult.Issues[index]);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawIssue(SceneExportIssue issue)
        {
            var messageType = ToMessageType(issue.Severity);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.HelpBox(issue.Message, messageType);
                if (issue.Context != null)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.ObjectField("Contexto", issue.Context, typeof(UnityEngine.Object), true);
                        if (GUILayout.Button("Ping", GUILayout.Width(64f)))
                        {
                            EditorGUIUtility.PingObject(issue.Context);
                            Selection.activeObject = issue.Context;
                        }
                    }
                }
            }
        }

        private void ValidateActiveScene()
        {
            var service = new SceneExportService();
            lastResult = service.ValidateActiveScene();
            ShowResultNotification("Validacion terminada");
            Repaint();
        }

        private void ExportActiveScene()
        {
            var service = new SceneExportService();
            lastResult = service.ExportActiveScene(contentPackVersion);
            ShowResultNotification(lastResult.Success ? "Exportacion completada" : "Exportacion con problemas");
            Repaint();
        }

        private void ShowResultNotification(string message)
        {
            ShowNotification(new GUIContent(message));
            Debug.Log(BuildLogSummary(message, lastResult));
        }

        private static string BuildLogSummary(string action, SceneExportResult result)
        {
            if (result == null)
            {
                return action + ".";
            }

            var details = new List<string>
            {
                action,
                "sceneId=" + (string.IsNullOrWhiteSpace(result.SceneId) ? "-" : result.SceneId),
                "success=" + result.Success,
                "issues=" + result.Issues.Count,
            };

            if (!string.IsNullOrWhiteSpace(result.SceneDataPath))
            {
                details.Add("output=" + result.SceneDataPath);
            }

            return string.Join(" | ", details);
        }

        private static MessageType ToMessageType(SceneExportIssueSeverity severity)
        {
            switch (severity)
            {
                case SceneExportIssueSeverity.Error:
                    return MessageType.Error;
                case SceneExportIssueSeverity.Warning:
                    return MessageType.Warning;
                default:
                    return MessageType.Info;
            }
        }
    }
}
