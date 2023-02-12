using System;
using System.Collections;
using System.Collections.Generic;
using SMLHelper.Assets;
using SMLHelper.Patchers;
using SMLHelper.Utility;
using UnityEngine;

namespace SMLHelper.Handlers;

/// <summary>
/// A handler for registering prefabs into the game.
/// </summary>
public static class PrefabHandler
{
    /// <summary>
    /// A collection of custom prefabs to add to the game.
    /// </summary>
    public static PrefabCollection Prefabs { get; } = new();

    internal static IEnumerator ProcessPrefabAsync(TaskResult<GameObject> gameObject, PrefabInfo info, PrefabFactoryAsync prefabFactory)
    {
        yield return prefabFactory(gameObject);
        
        var obj = gameObject.Get();
        var techType = info.TechType;
        var classId = info.ClassID;
        
        if (obj.activeInHierarchy) // inactive prefabs don't need to be removed by cache
            ModPrefabCache.AddPrefab(obj);

        obj.name = classId;

        if (techType != TechType.None)
        {

            if (obj.GetComponent<TechTag>() is { } tag)
            {
                tag.type = techType;
            }

            if (obj.GetComponent<Constructable>() is { } cs)
            {
                cs.techType = techType;
            }
        }

        if (obj.GetComponent<PrefabIdentifier>() is { } pid)
        {
            pid.ClassId = classId;
        }
    }
}

/// <summary>
/// Represents extension methods for the <see cref="PrefabCollection"/> class.
/// </summary>
public static class PrefabCollectionExtensions
{
    /// <summary>
    /// Registers a <see cref="CustomPrefab"/> into the game.
    /// </summary>
    /// <param name="collection">The collection to register to.</param>
    /// <param name="customPrefab">The custom prefab to register.</param>
    public static void RegisterPrefab(this PrefabCollection collection, ICustomPrefab customPrefab)
    {
        collection.Add(customPrefab.Info, customPrefab.Prefab);
    }

    /// <summary>
    /// Unregisters a <see cref="CustomPrefab"/> from the game.
    /// </summary>
    /// <param name="collection">The collection to unregister from.</param>
    /// <param name="customPrefab">The custom prefab to unregister.</param>
    public static void UnregisterPrefab(this PrefabCollection collection, ICustomPrefab customPrefab)
    {
        collection.Remove(customPrefab.Info);
    }
}

/// <summary>
/// Represents a collection of <see cref="PrefabInfo"/> as keys and prefab factory as values.
/// </summary>
public class PrefabCollection : IEnumerable<KeyValuePair<PrefabInfo, PrefabFactoryAsync>>
{
    private readonly Dictionary<PrefabInfo, PrefabFactoryAsync> _prefabs = new();
    
    private readonly Dictionary<string, PrefabInfo> _classIdPrefabs = new(StringComparer.InvariantCultureIgnoreCase);
    private readonly Dictionary<string, PrefabInfo> _fileNamePrefabs = new(StringComparer.InvariantCultureIgnoreCase);
    private readonly Dictionary<string, PrefabInfo> _techTypePrefabs = new(StringComparer.InvariantCultureIgnoreCase);

    /// <summary>
    /// Adds a prefab info with the function that constructs the game object into the game.
    /// </summary>
    /// <param name="info">The prefab info to register.</param>
    /// <param name="prefabFactory">The function that constructs the game object for this prefab info.</param>
    public void Add(PrefabInfo info, PrefabFactoryAsync prefabFactory)
    {
        if (_prefabs.ContainsKey(info))
        {
            InternalLogger.Error($"Another modded prefab already registered the following prefab: {info}");
            return;
        }

        if (_classIdPrefabs.ContainsKey(info.ClassID) || string.IsNullOrWhiteSpace(info.ClassID))
        {
            InternalLogger.Error($"Class ID is required and must be unique for prefab: {info}");
            return;
        }
        
        if (_fileNamePrefabs.ContainsKey(info.PrefabFileName) || string.IsNullOrWhiteSpace(info.PrefabFileName))
        {
            InternalLogger.Error($"PrefabFileName is required and must be unique for prefab: {info}");
            return;
        }
        
        _prefabs.Add(info, prefabFactory);
        _classIdPrefabs.Add(info.ClassID, info);
        _fileNamePrefabs.Add(info.PrefabFileName, info);
        _techTypePrefabs.Add(info.TechType.AsString(), info);
        CraftDataPatcher.ModPrefabsPatched = false;
    }

    /// <summary>
    /// Removes a prefab info from the game. This leads to unregistering the specified prefab info from the game.
    /// </summary>
    /// <param name="info">The prefab info to unregister.</param>
    /// <returns>True if the element is successfully found and removed; otherwise, false. This method returns false if the prefab info is not found.</returns>
    public bool Remove(PrefabInfo info)
    {
        var result = _prefabs.Remove(info);
        if (result)
        {
            _classIdPrefabs.Remove(info.ClassID);
            _fileNamePrefabs.Remove(info.PrefabFileName);
            _techTypePrefabs.Remove(info.TechType.AsString());
            CraftDataPatcher.ModPrefabsPatched = false;
        }

        return result;
    }

    /// <summary>
    /// Determines whether the provided prefab info is registered.
    /// </summary>
    /// <param name="info">The prefab info to look for</param>
    /// <returns>true if found; otherwise false.</returns>
    public bool ContainsPrefabInfo(PrefabInfo info)
    {
        return _prefabs.ContainsKey(info);
    }

    /// <summary>
    /// Gets the prefab factory associated with the provided info.
    /// </summary>
    /// <param name="info">The info of the prefab factory to get.</param>
    /// <param name="prefabFactory">The returned prefab factory. If nothing was found for the prefab info specified, this will be set to the default initialization instead.</param>
    /// <returns>True if found; otherwise false.</returns>
    public bool TryGetPrefabForInfo(PrefabInfo info, out PrefabFactoryAsync prefabFactory)
    {
        return _prefabs.TryGetValue(info, out prefabFactory);
    }

    /// <summary>
    /// Gets the prefab info associated with the provided class ID.
    /// </summary>
    /// <param name="classId">The class ID of the prefab info to get.</param>
    /// <param name="info">The returned prefab info. If nothing was found for the class ID specified, this will be set to the default initialization instead.</param>
    /// <returns>True if found; otherwise false.</returns>
    public bool TryGetInfoForClassId(string classId, out PrefabInfo info)
    {
        if (string.IsNullOrEmpty(classId))
        {
            info = default;
            return false;
        }

        return _classIdPrefabs.TryGetValue(classId, out info);
    }
    
    /// <summary>
    /// Gets the prefab info associated with the provided file name.
    /// </summary>
    /// <param name="fileName">The file name of the prefab info to get.</param>
    /// <param name="info">The returned prefab info. If nothing was found for the file name specified, this will be set to the default initialization instead.</param>
    /// <returns>True if found; otherwise false.</returns>
    public bool TryGetInfoForFileName(string fileName, out PrefabInfo info)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            info = default;
            return false;
        }

        return _fileNamePrefabs.TryGetValue(fileName, out info);
    }
    
    /// <summary>
    /// Gets the prefab info associated with the provided tech type.
    /// </summary>
    /// <param name="techType">The tech type of the prefab info to get.</param>
    /// <param name="info">The returned prefab info. If nothing was found for the tech type specified, this will be set to the default initialization instead.</param>
    /// <returns>True if found; otherwise false.</returns>
    public bool TryGetInfoForTechType(string techType, out PrefabInfo info)
    {
        if (string.IsNullOrEmpty(techType))
        {
            info = default;
            return false;
        }

        return _techTypePrefabs.TryGetValue(techType, out info);
    }

    IEnumerator<KeyValuePair<PrefabInfo, PrefabFactoryAsync>> IEnumerable<KeyValuePair<PrefabInfo, PrefabFactoryAsync>>.GetEnumerator()
    {
        return _prefabs.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return _prefabs.GetEnumerator();
    }
}
