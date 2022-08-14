using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class GitHelper : EditorWindow
{
    static readonly Vector2Int windowSize = new Vector2Int(250, 150);
    static readonly string targetDir = System.IO.Directory.GetCurrentDirectory() + "\\Assets";
    static string comment = "";

    [MenuItem("Git/Commit and Push")]
    static void CommitAndPushWindow() {
        comment = "";
        GitHelper window = CreateInstance<GitHelper>();
        window.position = new Rect(Screen.width / 2 + windowSize.x / 2, Screen.height / 2 + windowSize.y / 2, windowSize.x, windowSize.x);
        window.ShowPopup();
    }

    void OnGUI() {
        EditorGUILayout.LabelField("Commit comment: ", EditorStyles.wordWrappedLabel);
        comment = EditorGUILayout.TextArea(comment, GUILayout.Height(100));

        if (GUILayout.Button("Commit and Push")) {
            AddAll();
            Commit();
            Push();
            Close();
        }

        if (GUILayout.Button("Cancel")) {
            Close();
        }
    }

    void AddAll() {
        RunCmd("git add .");
    }

    void Commit() {
        RunCmd("git commit -m \"" + comment + "\"");
    }

    void Push() {
        RunCmd("git push");
    }

    void RunCmd(string command) {
        var processInfo = new System.Diagnostics.ProcessStartInfo("cmd.exe", "/c \"" + command + "\"") {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            WorkingDirectory = targetDir
        };

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        System.Diagnostics.Process p = System.Diagnostics.Process.Start(processInfo);
        p.OutputDataReceived += (sender, args) => sb.AppendLine(args.Data);
        p.BeginOutputReadLine();
        p.WaitForExit();
        Debug.Log(sb.ToString());
    }
}
