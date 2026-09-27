using System.Linq;
using Game.SOAP.Config;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.Editor
{
    // Generated from the authored catalog, so startup labels never pull in all item prefabs.
    public sealed class ItemCatalogMetadataBuildPreparation : IPreprocessBuildWithReport
    {
        public const string Path = "Assets/_Game/Content/Resources/Items/ItemCatalogMetadata.asset";
        public int callbackOrder => -100;
        public void OnPreprocessBuild(BuildReport report) => Prepare();

        public static ItemCatalogSO Prepare()
        {
            var source = ItemCatalogSO.Load(false);
            source.Validate();
            var metadata = AssetDatabase.LoadAssetAtPath<ItemCatalogSO>(Path);
            var isNew = metadata == null;
            if (isNew) metadata = ScriptableObject.CreateInstance<ItemCatalogSO>();
            metadata.categories = source.categories.Select(c => new ItemCatalogSO.Category {
                id = c.id, label = c.label, enabled = c.enabled,
                items = c.items.Select(i => new ItemCatalogSO.Item {
                    id = i.id, displayName = i.displayName, enabled = i.enabled
                }).ToList()
            }).ToList();
            if (isNew) AssetDatabase.CreateAsset(metadata, Path);
            else EditorUtility.SetDirty(metadata);
            AssetDatabase.SaveAssets();
            return metadata;
        }
    }
}
