#if TMP_PRESENT
using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Codely.TmpChineseFont
{
    public sealed class TmpChineseFontReplaceResult
    {
        public string Status;
        public int ScenesScanned;
        public int PrefabsScanned;
        public int ComponentsReplaced;
        public readonly List<string> Errors = new List<string>();

        public override string ToString()
        {
            return $"{Status} scenesScanned={ScenesScanned} prefabsScanned={PrefabsScanned} " +
                   $"componentsReplaced={ComponentsReplaced} errors={Errors.Count}";
        }
    }

    public static class TmpChineseFontReplacer
    {
        public static TmpChineseFontReplaceResult Replace(
            TMP_FontAsset previous,
            TMP_FontAsset current,
            string[] searchRoots)
        {
            if (searchRoots == null || searchRoots.Length == 0)
                searchRoots = new[] { "Assets" };

            var result = new TmpChineseFontReplaceResult();
            ReplaceInPrefabs(previous, current, searchRoots, result);
            ReplaceInScenes(previous, current, searchRoots, result);
            ReplaceInLoadedScenes(previous, current, searchRoots, result);
            result.Status = result.ComponentsReplaced == 0
                ? TmpChineseFontStatus.NoOldFontReferences
                : TmpChineseFontStatus.ReplaceDone;
            return result;
        }

        private static void ReplaceInPrefabs(
            TMP_FontAsset previous,
            TMP_FontAsset current,
            string[] searchRoots,
            TmpChineseFontReplaceResult result)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", searchRoots))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (ShouldSkip(path))
                    continue;

                result.PrefabsScanned++;
                GameObject contents = null;
                try
                {
                    contents = PrefabUtility.LoadPrefabContents(path);
                    if (ReplaceComponents(
                            contents.GetComponentsInChildren<TMP_Text>(true),
                            previous,
                            current,
                            result) == 0)
                        continue;

                    PrefabUtility.SaveAsPrefabAsset(contents, path, out var saved);
                    if (!saved)
                        result.Errors.Add(path);
                }
                catch (Exception)
                {
                    result.Errors.Add(path);
                }
                finally
                {
                    if (contents != null)
                        PrefabUtility.UnloadPrefabContents(contents);
                }
            }
        }

        private static void ReplaceInScenes(
            TMP_FontAsset previous,
            TMP_FontAsset current,
            string[] searchRoots,
            TmpChineseFontReplaceResult result)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Scene", searchRoots))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (ShouldSkip(path))
                    continue;

                result.ScenesScanned++;
                var scene = SceneManager.GetSceneByPath(path);
                var openedHere = !scene.IsValid() || !scene.isLoaded;
                try
                {
                    if (openedHere)
                        scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

                    var replaced = 0;
                    foreach (var root in scene.GetRootGameObjects())
                    {
                        replaced += ReplaceComponents(
                            root.GetComponentsInChildren<TMP_Text>(true),
                            previous,
                            current,
                            result);
                    }

                    if (replaced == 0)
                        continue;

                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene))
                        result.Errors.Add(path);
                }
                catch (Exception)
                {
                    result.Errors.Add(path);
                }
                finally
                {
                    if (openedHere && scene.IsValid() && scene.isLoaded)
                        EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void ReplaceInLoadedScenes(
            TMP_FontAsset previous,
            TMP_FontAsset current,
            string[] searchRoots,
            TmpChineseFontReplaceResult result)
        {
            var dirtyScenes = new List<Scene>();
            foreach (var component in UnityEngine.Object.FindObjectsOfType<TMP_Text>(true))
            {
                var scene = component.gameObject.scene;
                if (!scene.IsValid() ||
                    !scene.isLoaded ||
                    ShouldSkip(scene.path) ||
                    !IsInSearchRoots(scene.path, searchRoots) ||
                    component.font != previous)
                    continue;

                component.font = current;
                EditorUtility.SetDirty(component);
                result.ComponentsReplaced++;
                EditorSceneManager.MarkSceneDirty(scene);
                if (!ContainsScene(dirtyScenes, scene))
                    dirtyScenes.Add(scene);
            }

            foreach (var scene in dirtyScenes)
            {
                try
                {
                    if (!EditorSceneManager.SaveScene(scene))
                        result.Errors.Add(scene.path);
                }
                catch (Exception)
                {
                    result.Errors.Add(scene.path);
                }
            }
        }

        private static int ReplaceComponents(
            TMP_Text[] components,
            TMP_FontAsset previous,
            TMP_FontAsset current,
            TmpChineseFontReplaceResult result)
        {
            var replaced = 0;
            foreach (var component in components)
            {
                if (component.font != previous)
                    continue;

                component.font = current;
                EditorUtility.SetDirty(component);
                result.ComponentsReplaced++;
                replaced++;
            }

            return replaced;
        }

        private static bool ShouldSkip(string path)
        {
            return string.IsNullOrEmpty(path) ||
                   path.StartsWith("Packages/", StringComparison.Ordinal) ||
                   path.StartsWith("Library/", StringComparison.Ordinal);
        }

        private static bool IsInSearchRoots(string path, string[] searchRoots)
        {
            var normalizedPath = path.Replace('\\', '/').TrimEnd('/');
            foreach (var searchRoot in searchRoots)
            {
                if (string.IsNullOrEmpty(searchRoot))
                    continue;

                var normalizedRoot = searchRoot.Replace('\\', '/').TrimEnd('/');
                if (normalizedPath == normalizedRoot ||
                    normalizedPath.StartsWith(normalizedRoot + "/", StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static bool ContainsScene(List<Scene> scenes, Scene candidate)
        {
            foreach (var scene in scenes)
            {
                if (scene.handle == candidate.handle)
                    return true;
            }

            return false;
        }
    }
}
#endif
