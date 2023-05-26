using Nautilus.Utility;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nautilus.Assets;

/// <summary>
/// Class used by the prefab system to store GameObjects.
/// Objects in the cache are inactive because they are placed within an inactive parent object.
/// </summary>
public static class ModPrefabCache
{
    private static ModPrefabCacheInstance _cacheInstance;

    internal record struct Entry(string ClassId, GameObject Prefab);

    /// <summary> Adds the given prefab to the cache. </summary>
    /// <param name="prefab"> The prefab object that is disabled and cached. </param>
    public static void AddPrefab(GameObject prefab)
    {
        EnsureCacheExists();

        _cacheInstance.EnterPrefabIntoCache(prefab);
    }

    /// <summary>
    /// Determines if a prefab is already cached, searching by class id.
    /// </summary>
    /// <param name="classId">The class id to search for.</param>
    /// <returns>True if a prefab by the given <paramref name="classId"/> exists in the cache, otherwise false.</returns>
    public static bool IsPrefabCached(string classId)
    {
        if (_cacheInstance == null)
            return false;

        return _cacheInstance.Entries.ContainsKey(classId);
    }

    /// <summary>
    /// Any prefab with the matching <paramref name="classId"/> will be removed from the cache.
    /// </summary>
    /// <param name="classId">The class id of the prefab that will be removed.</param>
    public static void RemovePrefabFromCache(string classId)
    {
        if (_cacheInstance == null)
            return;

        if (_cacheInstance.Entries.TryGetValue(classId, out var entry))
        {
            _cacheInstance.RemoveCachedPrefab(entry);
        }
    }

    /// <summary>
    /// Attempts to fetch a prefab from the cache by its <paramref name="classId"/>. The <paramref name="prefab"/> out parameter is set to the prefab, if any was found.
    /// </summary>
    /// <param name="classId">The class id of the prefab we are searching for.</param>
    /// <param name="prefab">The prefab that may or may not be found.</param>
    /// <returns>True if the prefab was found in the cache, otherwise false.</returns>
    public static bool TryGetPrefabFromCache(string classId, out GameObject prefab)
    {
        if (_cacheInstance == null)
        {
            prefab = null;
            return false;
        }

        if (_cacheInstance.Entries.TryGetValue(classId, out var found))
        {
            prefab = found.Prefab;
            return prefab != null;
        }

        prefab = null;
        return false;
    }

    private static void EnsureCacheExists()
    {
        if (_cacheInstance != null)
            return;
        _cacheInstance = new GameObject("Nautilus.PrefabCache").AddComponent<ModPrefabCacheInstance>();
    }
}
internal class ModPrefabCacheInstance : MonoBehaviour
{
    public Dictionary<string, ModPrefabCache.Entry> Entries { get; } = new Dictionary<string, ModPrefabCache.Entry>();

    private Transform _prefabRoot;

    private void Awake()
    {
        _prefabRoot = new GameObject("PrefabRoot").transform;
        _prefabRoot.parent = transform;
        _prefabRoot.gameObject.SetActive(false);
    }

    public void EnterPrefabIntoCache(GameObject prefab)
    {
        prefab.transform.parent = _prefabRoot;

        var prefabIdentifier = prefab.GetComponent<PrefabIdentifier>();

        if (prefabIdentifier == null)
        {
            InternalLogger.Warn($"ModPrefabCache: prefab is missing a PrefabIdentifier component! Unable to add to cache.");
            return;
        }

        if (!Entries.ContainsKey(prefabIdentifier.classId))
        {
            Entries.Add(prefabIdentifier.classId, new ModPrefabCache.Entry(prefabIdentifier.classId, prefab));
            InternalLogger.Debug($"ModPrefabCache: adding prefab {prefab}");
        }
        else // this should never happen
        {
            InternalLogger.Warn($"ModPrefabCache: prefab {prefabIdentifier.classId} already existed in cache!");
        }
    }

    public void RemoveCachedPrefab(ModPrefabCache.Entry entry)
    {
        Destroy(entry.Prefab);
        Entries.Remove(entry.ClassId);
    }
}