﻿using Harmony;
using SMLHelper.V2.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SMLHelper.V2.Patchers
{
    public class CraftTreeTypePatcher
    {
        private static readonly FieldInfo CachedEnumString_valueToString =
            typeof(CachedEnumString<CraftTree.Type>).GetField("valueToString", BindingFlags.NonPublic | BindingFlags.Instance);

        internal const int startingIndex = 11; // The default CraftTree.Type contains indexes 0 through 10
        internal static string CallerName = null;

        internal static readonly EnumCacheManager<CraftTree.Type> cacheManager = new EnumCacheManager<CraftTree.Type>("CraftTreeType", startingIndex);

        #region Adding  CraftTree Types

        /// <summary>
        /// Your first method call to start a new custom crafting tree.
        /// Creating a new CraftTree only makes sense if you're going to use it in a new type of GhostCrafter/Fabricator.
        /// </summary>
        /// <param name="name">The name for the new <see cref="CraftTree.Type"/> enum.</param>
        /// <param name="craftTreeType">The new enum instance for your custom craft tree.</param>
        /// <returns>A new root node for your custom craft tree.</returns>
        /// <remarks>This node is automatically assigned to <see cref="CraftTreePatcher.CustomTrees"/>.</remarks>
        public static CustomCraftTreeRoot CreateCustomCraftTreeAndType(string name, out CraftTree.Type craftTreeType)
        {
            var cache = cacheManager.GetCacheForTypeName(name);

            if (cache == null)
            {
                cache = new EnumTypeCache()
                {
                    Name = name,
                    Index = cacheManager.GetNextFreeIndex()
                };
            }

            if (cacheManager.IsIndexConflicting(cache.Index))
                cache.Index = cacheManager.GetNextFreeIndex();

            craftTreeType = (CraftTree.Type)cache.Index;

            cacheManager.customEnumTypes.Add(craftTreeType, cache);

            CallerName = CallerName ?? Assembly.GetCallingAssembly().GetName().Name;
            Logger.Log("Successfully added CraftTree Type: \"{0}\" to Index: \"{1}\" for mod \"{2}\"", name, cache.Index, CallerName);
            CallerName = null;

            cacheManager.SaveCache();

            var customTreeRoot = new CustomCraftTreeRoot(craftTreeType, name);

            CraftTreePatcher.CustomTrees[craftTreeType] = customTreeRoot;

            return customTreeRoot;
        }

        #endregion

        #region Patches

        internal static void Patch(HarmonyInstance harmony)
        {
            var enumType = typeof(Enum);
            var thisType = typeof(CraftTreeTypePatcher);
            var techTypeType = typeof(CraftTree.Type);

            harmony.Patch(enumType.GetMethod("GetValues", BindingFlags.Public | BindingFlags.Static), null,
                new HarmonyMethod(thisType.GetMethod("Postfix_GetValues", BindingFlags.NonPublic | BindingFlags.Static)));

            harmony.Patch(enumType.GetMethod("IsDefined", BindingFlags.Public | BindingFlags.Static),
                new HarmonyMethod(thisType.GetMethod("Prefix_IsDefined", BindingFlags.NonPublic | BindingFlags.Static)), null);

            harmony.Patch(enumType.GetMethod("Parse", new Type[] { typeof(Type), typeof(string), typeof(bool) }),
                new HarmonyMethod(thisType.GetMethod("Prefix_Parse", BindingFlags.NonPublic | BindingFlags.Static)), null);

            harmony.Patch(techTypeType.GetMethod("ToString", new Type[0]),
                new HarmonyMethod(thisType.GetMethod("Prefix_ToString", BindingFlags.NonPublic | BindingFlags.Static)), null);

            Logger.Log("CraftTreeTypePatcher is done.");
        }

        internal static void Postfix_GetValues(Type enumType, ref Array __result)
        {
            if (enumType.Equals(typeof(CraftTree.Type)))
            {
                var listArray = new List<CraftTree.Type>();
                foreach (var obj in __result)
                {
                    listArray.Add((CraftTree.Type)obj);
                }

                __result = listArray
                    .Concat(cacheManager.customEnumTypes.Keys)
                    .ToArray();
            }
        }

        internal static bool Prefix_IsDefined(Type enumType, object value, ref bool __result)
        {
            if (enumType.Equals(typeof(CraftTree.Type)))
            {
                if (cacheManager.customEnumTypes.Keys.Contains((CraftTree.Type)value))
                {
                    __result = true;
                    return false;
                }
            }

            return true;
        }

        internal static bool Prefix_Parse(Type enumType, string value, bool ignoreCase, ref object __result)
        {
            if (enumType.Equals(typeof(CraftTree.Type)))
            {
                foreach (var techType in cacheManager.customEnumTypes)
                {
                    if (value.Equals(techType.Value.Name, ignoreCase ? StringComparison.InvariantCultureIgnoreCase : StringComparison.InvariantCulture))
                    {
                        __result = techType.Key;
                        return false;
                    }
                }
            }

            return true;
        }

        internal static bool Prefix_ToString(Enum __instance, ref string __result)
        {
            if (__instance.GetType().Equals(typeof(CraftTree.Type)))
            {
                foreach (var techType in cacheManager.customEnumTypes)
                {
                    if (__instance.Equals(techType.Key))
                    {
                        __result = techType.Value.Name;
                        return false;
                    }
                }
            }

            return true;
        }

        #endregion
    }
}
