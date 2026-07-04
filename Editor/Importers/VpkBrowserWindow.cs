using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Source2Unity.Formats.Vpk;
using Source2Unity.Formats.Vpk.Parsers;
using UnityEditor;
using UnityEngine;

namespace Source2Unity.Editor.Importers
{
    public class VpkBrowserWindow : EditorWindow
    {
        private VpkArchive _archive;
        private VpkParseResult _result;
        private string _currentPath = "";
        private Vector2 _scrollPos;
        private Vector2 _previewScrollPos;
        private string _searchFilter = "";
        private string _selectedFilePath;
        private byte[] _selectedFileData;
        private string _statusMessage = "";
        private List<VpkFileEntry> _filteredEntries;

        [MenuItem("Tools/Source2Unity/VPK Browser")]
        public static void ShowWindow()
        {
            GetWindow<VpkBrowserWindow>("VPK Browser");
        }

        private void OnGUI()
        {
            DrawToolbar();

            if (_result == null)
            {
                EditorGUILayout.HelpBox("Open a VPK directory file (_dir.vpk) to browse its contents.", MessageType.Info);
                return;
            }

            EditorGUILayout.BeginHorizontal();
            DrawFileList();
            DrawPreviewPanel();
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_statusMessage))
                EditorGUILayout.HelpBox(_statusMessage, MessageType.Info);
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("Open VPK", EditorStyles.toolbarButton, GUILayout.Width(80)))
                OpenVpk();

            if (_result != null)
            {
                GUILayout.Label($"v{_result.Version} | {GetTotalFileCount()} files", EditorStyles.toolbarButton);

                GUILayout.FlexibleSpace();

                EditorGUI.BeginChangeCheck();
                _searchFilter = EditorGUILayout.TextField(_searchFilter, EditorStyles.toolbarSearchField, GUILayout.Width(200));
                if (EditorGUI.EndChangeCheck())
                    RefreshFilteredEntries();

                if (GUILayout.Button("Extract All", EditorStyles.toolbarButton, GUILayout.Width(80)))
                    ExtractAll();
            }
            else
            {
                GUILayout.FlexibleSpace();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawFileList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.55f));
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            if (_filteredEntries != null)
            {
                foreach (var entry in _filteredEntries)
                {
                    string fullPath = entry.GetFullPath();
                    bool isSelected = fullPath == _selectedFilePath;

                    var style = isSelected ? EditorStyles.boldLabel : EditorStyles.label;
                    if (GUILayout.Button(fullPath, style))
                        SelectEntry(entry);
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawPreviewPanel()
        {
            EditorGUILayout.BeginVertical();
            _previewScrollPos = EditorGUILayout.BeginScrollView(_previewScrollPos);

            if (!string.IsNullOrEmpty(_selectedFilePath))
            {
                EditorGUILayout.LabelField("Selected:", _selectedFilePath, EditorStyles.boldLabel);

                if (_selectedFileData != null)
                {
                    EditorGUILayout.LabelField("Size:", $"{_selectedFileData.Length:N0} bytes");

                    string ext = Path.GetExtension(_selectedFilePath).ToLowerInvariant();
                    if (ext == ".txt" || ext == ".cfg" || ext == ".vmt" || ext == ".res" || ext == ".qc")
                    {
                        string text = System.Text.Encoding.UTF8.GetString(_selectedFileData);
                        EditorGUILayout.TextArea(text, GUILayout.ExpandHeight(true));
                    }
                    else
                    {
                        EditorGUILayout.HelpBox($"Binary file ({ext}). Use 'Extract' to save to disk.", MessageType.None);
                    }

                    if (GUILayout.Button("Extract File"))
                        ExtractSingleFile(_selectedFilePath, _selectedFileData);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Select a file from the list to preview.", MessageType.None);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void OpenVpk()
        {
            string path = EditorUtility.OpenFilePanel("Open VPK Directory", "", "vpk");
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                _archive?.Dispose();
                _archive = new VpkArchive();
                _result = _archive.Read(path);
                _selectedFilePath = null;
                _selectedFileData = null;
                _searchFilter = "";
                RefreshFilteredEntries();
                _statusMessage = $"Loaded: {path}";
            }
            catch (Exception ex)
            {
                _statusMessage = $"Error: {ex.Message}";
                _result = null;
            }
        }

        private void SelectEntry(VpkFileEntry entry)
        {
            _selectedFilePath = entry.GetFullPath();
            try
            {
                _selectedFileData = _archive.ReadEntry(entry);
            }
            catch (Exception ex)
            {
                _selectedFileData = null;
                _statusMessage = $"Error reading file: {ex.Message}";
            }
        }

        private void ExtractAll()
        {
            string folder = EditorUtility.OpenFolderPanel("Extract VPK Contents", "", "");
            if (string.IsNullOrEmpty(folder)) return;

            int count = 0;
            foreach (var group in _result.Entries)
            {
                foreach (var entry in group.Value)
                {
                    try
                    {
                        string fullPath = entry.GetFullPath();
                        string outputPath = Path.Combine(folder, fullPath.Replace('/', Path.DirectorySeparatorChar));
                        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                        var data = _archive.ReadEntry(entry);
                        File.WriteAllBytes(outputPath, data);
                        count++;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[Source2Unity] Failed to extract {entry.GetFullPath()}: {ex.Message}");
                    }
                }
            }

            _statusMessage = $"Extracted {count} files to {folder}";
            EditorUtility.DisplayDialog("Extraction Complete", $"Extracted {count} files.", "OK");
        }

        private void ExtractSingleFile(string filePath, byte[] data)
        {
            string defaultName = Path.GetFileName(filePath);
            string ext = Path.GetExtension(filePath).TrimStart('.');
            string savePath = EditorUtility.SaveFilePanel("Save File", "", defaultName, ext);
            if (string.IsNullOrEmpty(savePath)) return;

            File.WriteAllBytes(savePath, data);
            _statusMessage = $"Saved: {savePath}";
        }

        private void RefreshFilteredEntries()
        {
            if (_result == null)
            {
                _filteredEntries = null;
                return;
            }

            var all = new List<VpkFileEntry>();
            foreach (var group in _result.Entries)
                all.AddRange(group.Value);

            if (string.IsNullOrEmpty(_searchFilter))
            {
                _filteredEntries = all.OrderBy(e => e.GetFullPath()).ToList();
            }
            else
            {
                string filter = _searchFilter.ToLowerInvariant();
                _filteredEntries = all
                    .Where(e => e.GetFullPath().ToLowerInvariant().Contains(filter))
                    .OrderBy(e => e.GetFullPath())
                    .ToList();
            }
        }

        private int GetTotalFileCount()
        {
            if (_result?.Entries == null) return 0;
            int count = 0;
            foreach (var group in _result.Entries)
                count += group.Value.Count;
            return count;
        }

        private void OnDestroy()
        {
            _archive?.Dispose();
        }
    }
}
