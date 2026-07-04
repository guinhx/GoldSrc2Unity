using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Source2Unity.Converters.Mdl;
using Source2Unity.Converters.Pipeline;
using Source2Unity.Converters.Vmt;
using Source2Unity.Converters.Vtf;
using Source2Unity.Formats.Common;
using Source2Unity.Formats.Mdl;
using Source2Unity.Formats.Vmt;
using Source2Unity.Formats.Vpk;
using Source2Unity.Formats.Vpk.Parsers;
using Source2Unity.Formats.Vtf;
using UnityEditor;
using UnityEngine;

namespace Source2Unity.Editor.Importers
{
    public class VpkBrowserWindow : EditorWindow
    {
        private const int TextPreviewMaxBytes = 256 * 1024;

        private VpkArchive _archive;
        private VpkParseResult _result;
        private Vector2 _scrollPos;
        private Vector2 _previewScrollPos;
        private string _searchFilter = "";
        private string _selectedFilePath;
        private VpkFileEntry _selectedEntry;
        private byte[] _selectedFileData;
        private string _statusMessage = "";
        private List<VpkFileEntry> _filteredEntries;
        private GameObject _previewInstance;
        private Texture2D _previewTexture;
        private Material _previewMaterial;

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

            if (!string.IsNullOrEmpty(_selectedFilePath) && _selectedEntry != null)
            {
                EditorGUILayout.LabelField("Selected:", _selectedFilePath, EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Size:", $"{_selectedEntry.TotalLength:N0} bytes");

                string ext = Path.GetExtension(_selectedFilePath).ToLowerInvariant();

                if (IsTextExtension(ext))
                {
                    EnsureTextPreviewLoaded();
                    if (_selectedFileData != null)
                    {
                        string text = System.Text.Encoding.UTF8.GetString(_selectedFileData);
                        EditorGUILayout.TextArea(text, GUILayout.ExpandHeight(true));
                    }
                }
                else if (ext == ".mdl")
                {
                    EditorGUILayout.HelpBox("GoldSrc MDL model. Preview loads the full model pipeline (mesh, bones, animations).", MessageType.Info);

                    if (GUILayout.Button("Preview in Scene"))
                        PreviewMdlInScene();
                }
                else if (ext == ".vtf")
                {
                    EditorGUILayout.HelpBox("Source VTF texture. Builds cubemaps (6 faces) and animation frames when present.", MessageType.Info);

                    if (GUILayout.Button("Preview Texture"))
                        PreviewVtf();

                    DrawTexturePreview();
                }
                else if (ext == ".vmt")
                {
                    EditorGUILayout.HelpBox("Source VMT material. Preview builds a Unity material with referenced VTF textures from the VPK.", MessageType.Info);

                    if (GUILayout.Button("Preview Material"))
                        PreviewVmtInScene();
                }
                else
                {
                    EditorGUILayout.HelpBox($"Binary file ({ext}). Use 'Extract' to save to disk.", MessageType.None);
                }

                if (GUILayout.Button("Extract File"))
                    ExtractSelectedEntry();
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
                ClearPreviewAssets();
                _archive?.Dispose();
                _archive = new VpkArchive();
                _result = _archive.Read(path);
                ClearSelection();
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
            _selectedEntry = entry;
            _selectedFilePath = entry.GetFullPath();
            _selectedFileData = null;
            _statusMessage = null;
            ClearPreviewAssets();
        }

        private void EnsureTextPreviewLoaded()
        {
            if (_selectedFileData != null || _selectedEntry == null || _archive == null)
                return;

            try
            {
                if (_selectedEntry.TotalLength > TextPreviewMaxBytes)
                {
                    _statusMessage = $"Text preview limited to {TextPreviewMaxBytes:N0} bytes. Use Extract for the full file.";
                    _selectedFileData = ReadEntryBytesLimited(_archive, _selectedEntry, TextPreviewMaxBytes);
                    return;
                }

                _selectedFileData = ReadEntryBytes(_archive, _selectedEntry);
            }
            catch (Exception ex)
            {
                _selectedFileData = null;
                _statusMessage = $"Error reading file: {ex.Message}";
            }
        }

        private void PreviewMdlInScene()
        {
            if (_archive == null || _selectedEntry == null)
                return;

            try
            {
                ClearPreviewAssets();

                var resolver = new CompositeContentResolver();
                resolver.Add(new VpkContentResolver(_archive));

                var parseResult = new MdlFile().Read(_selectedFilePath, resolver);
                var buildResult = MdlModelBuilder.Build(parseResult);

                _previewInstance = buildResult.Root;
                _previewInstance.name = $"[VPK Preview] {Path.GetFileNameWithoutExtension(_selectedFilePath)}";

                Selection.activeGameObject = _previewInstance;
                if (SceneView.lastActiveSceneView != null)
                    SceneView.lastActiveSceneView.FrameSelected();

                _statusMessage = $"Previewing MDL: {_selectedFilePath}";
            }
            catch (Exception ex)
            {
                _statusMessage = $"MDL preview failed: {ex.Message}";
                Debug.LogException(ex);
            }
        }

        private void PreviewVtf()
        {
            if (_archive == null || _selectedEntry == null)
                return;

            try
            {
                ClearPreviewAssets();

                using var stream = _archive.ReadEntryStream(_selectedEntry);
                var parseResult = new VtfFile().Read(stream);
                var build = VtfTextureBuilder.Build(parseResult, new VtfTextureBuildOptions
                {
                    OnWarning = message => _statusMessage = message
                });
                _previewTexture = build.Texture;
                _previewTexture.name = Path.GetFileNameWithoutExtension(_selectedFilePath);

                string extras = build.IsCubemap ? " | cubemap" : string.Empty;
                if (build.IsAnimated)
                    extras += $" | {build.AnimationFrames?.Length ?? 0} frames";

                _statusMessage = $"Previewing VTF: {_selectedFilePath} ({parseResult.Width}x{parseResult.Height}){extras}";
            }
            catch (Exception ex)
            {
                _statusMessage = $"VTF preview failed: {ex.Message}";
                Debug.LogException(ex);
            }
        }

        private void PreviewVmtInScene()
        {
            if (_archive == null || _selectedEntry == null)
                return;

            try
            {
                ClearPreviewAssets();

                var resolver = new CompositeContentResolver();
                resolver.Add(new VpkContentResolver(_archive));
                var context = new AssetLoadContext(resolver);

                var parseResult = new VmtFile().Read(_selectedFilePath, resolver);
                var buildResult = VmtMaterialBuilder.Build(parseResult, context);

                _previewMaterial = buildResult.Material;
                _previewTexture = buildResult.BaseTexture;

                _previewInstance = GameObject.CreatePrimitive(PrimitiveType.Quad);
                _previewInstance.name = $"[VPK Preview] {Path.GetFileNameWithoutExtension(_selectedFilePath)}";
                _previewInstance.GetComponent<Renderer>().sharedMaterial = _previewMaterial;

                Selection.activeGameObject = _previewInstance;
                if (SceneView.lastActiveSceneView != null)
                    SceneView.lastActiveSceneView.FrameSelected();

                _statusMessage = $"Previewing VMT: {_selectedFilePath}";
            }
            catch (Exception ex)
            {
                _statusMessage = $"VMT preview failed: {ex.Message}";
                Debug.LogException(ex);
            }
        }

        private void DrawTexturePreview()
        {
            if (_previewTexture == null)
                return;

            float maxSize = Mathf.Min(position.width * 0.4f, 256f);
            float aspect = (float)_previewTexture.width / _previewTexture.height;
            float width = aspect >= 1f ? maxSize : maxSize * aspect;
            float height = aspect >= 1f ? maxSize / aspect : maxSize;
            var rect = GUILayoutUtility.GetRect(width, height, GUILayout.ExpandWidth(false));
            EditorGUI.DrawPreviewTexture(rect, _previewTexture, null, ScaleMode.ScaleToFit);
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
                        CopyEntryToFile(_archive, entry, outputPath);
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

        private void ExtractSelectedEntry()
        {
            if (_selectedEntry == null)
                return;

            string defaultName = Path.GetFileName(_selectedFilePath);
            string ext = Path.GetExtension(_selectedFilePath).TrimStart('.');
            string savePath = EditorUtility.SaveFilePanel("Save File", "", defaultName, ext);
            if (string.IsNullOrEmpty(savePath))
                return;

            try
            {
                CopyEntryToFile(_archive, _selectedEntry, savePath);
                _statusMessage = $"Saved: {savePath}";
            }
            catch (Exception ex)
            {
                _statusMessage = $"Extract failed: {ex.Message}";
            }
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

        private void ClearSelection()
        {
            _selectedFilePath = null;
            _selectedEntry = null;
            _selectedFileData = null;
        }

        private void DestroyPreviewInstance()
        {
            if (_previewInstance != null)
            {
                DestroyImmediate(_previewInstance);
                _previewInstance = null;
            }
        }

        private void ClearPreviewAssets()
        {
            DestroyPreviewInstance();

            if (_previewTexture != null)
            {
                DestroyImmediate(_previewTexture);
                _previewTexture = null;
            }

            if (_previewMaterial != null)
            {
                DestroyImmediate(_previewMaterial);
                _previewMaterial = null;
            }
        }

        private static bool IsTextExtension(string ext)
        {
            return ext == ".txt" || ext == ".cfg" || ext == ".vmt" || ext == ".res" || ext == ".qc";
        }

        private static void CopyEntryToFile(VpkArchive archive, VpkFileEntry entry, string outputPath)
        {
            using var stream = archive.ReadEntryStream(entry);
            using var fileStream = File.Create(outputPath);
            stream.CopyTo(fileStream);
        }

        private static byte[] ReadEntryBytes(VpkArchive archive, VpkFileEntry entry)
        {
            using var stream = archive.ReadEntryStream(entry);
            return ReadStreamToBytes(stream, (int)entry.TotalLength);
        }

        private static byte[] ReadEntryBytesLimited(VpkArchive archive, VpkFileEntry entry, int maxBytes)
        {
            using var stream = archive.ReadEntryStream(entry);
            int length = (int)Math.Min(entry.TotalLength, maxBytes);
            return ReadStreamToBytes(stream, length);
        }

        private static byte[] ReadStreamToBytes(Stream stream, int length)
        {
            var buffer = new byte[length];
            int offset = 0;
            while (offset < length)
            {
                int read = stream.Read(buffer, offset, length - offset);
                if (read == 0)
                    break;
                offset += read;
            }

            if (offset == length)
                return buffer;

            var trimmed = new byte[offset];
            Buffer.BlockCopy(buffer, 0, trimmed, 0, offset);
            return trimmed;
        }

        private void OnDestroy()
        {
            ClearPreviewAssets();
            _archive?.Dispose();
        }
    }
}
