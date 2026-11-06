using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.UI;

namespace TGJ.PaperCollage.Editor
{
    /// <summary>Replaces only the seven art Images on a caller-provided scene-one UI clone.</summary>
    public static class Scene2MotelArt
    {
        const string Handoff = "Assets/Scene2_Motel_Unity_Handoff";
        const string BoundsPath = Handoff + "/Design/Scene2Motel/sprite_bounds.json";
        const string LogicalArt = "Assets/ThatGameJam2026/Scene2Motel/Art/";
        static readonly string[] PreviousIds = { "phone", "bag", "key", "ticket", "book", "wallet" };
        static readonly string[] MotelIds = { "violin_case", "beer", "coat", "open_suitcase", "music_score", "biscuit_tin" };

        [Serializable] sealed class BoundsFile { public ArtBounds[] assets; }
        [Serializable] sealed class ArtBounds
        {
            public string path;
            public int[] canvas_size;
            public BoundsRect sprite_rect_unity;
        }
        [Serializable] sealed class BoundsRect
        {
            public float x, y, width, height;
            public Rect Rect => new Rect(x, y, width, height);
        }

        /// <summary>
        /// Caller owns cloning, palette, scene registration and scene/prefab saves.
        /// Original PNG bytes and all existing RectTransforms/hit areas/frames are untouched.
        /// Only the seven corresponding TextureImporter metadata records are updated.
        /// </summary>
        public static void Apply(GameObject clonedRoot)
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Stop Play before replacing motel art.");
            Require(clonedRoot && !EditorUtility.IsPersistent(clonedRoot), "Pass the instantiated UI clone, not a prefab asset.");
            var design = clonedRoot.transform.Find("Design_1920x1080");
            Require(design, "The UI clone has no Design_1920x1080 group.");
            var items = design.Find("InventoryPanel/Items");
            var room = design.Find("SceneFrame/motel_room") ?? design.Find("SceneFrame/concert_hall");
            Require(items && room && room.GetComponent<Image>(), "Missing existing item group or central art Image.");
            var selection = items.GetComponent<PaperInventorySelection>();
            Require(selection, "The clone must inherit the existing inventory selection handler.");
            var itemObjects = new Transform[MotelIds.Length];
            for (int i = 0; i < MotelIds.Length; i++)
            {
                itemObjects[i] = items.Find(MotelIds[i]) ?? items.Find(PreviousIds[i]);
                Require(itemObjects[i] && itemObjects[i].GetComponent<Image>(), "Missing original item Image: " + PreviousIds[i]);
                Require(itemObjects[i].Find("InventoryHitArea") && itemObjects[i].Find("SelectionFrame"),
                    "The clone must inherit the existing hit rectangle and frame for " + PreviousIds[i]);
            }

            var bounds = JsonUtility.FromJson<BoundsFile>(File.ReadAllText(BoundsPath));
            Require(bounds != null && bounds.assets != null, "Invalid sprite_bounds.json.");
            var roomBounds = FindBounds(bounds, "Room/room_lights_on.png");
            var itemBounds = MotelIds.Select(id => FindBounds(bounds, "Props/" + id + ".png")).ToArray();
            // Resolve every required path before changing importers or the cloned hierarchy.
            foreach (var entry in new[] { roomBounds }.Concat(itemBounds))
                Require(File.Exists(ActualPath(entry)), "Missing supplied original PNG: " + ActualPath(entry));

            var factories = new SpriteDataProviderFactories();
            factories.Init();
            var roomSprite = ImportTrimmedSprite(roomBounds, factories);
            var itemSprites = itemBounds.Select(entry => ImportTrimmedSprite(entry, factories)).ToArray();

            Undo.RegisterFullObjectHierarchyUndo(clonedRoot, "Apply original motel room and inventory art");
            SetImage(room.GetComponent<Image>(), roomSprite);
            room.name = "motel_room";
            for (int i = 0; i < itemObjects.Length; i++)
            {
                SetImage(itemObjects[i].GetComponent<Image>(), itemSprites[i]);
                itemObjects[i].name = MotelIds[i];
            }
            selection.Configure(items);
            selection.SelectItem("open_suitcase");
            Canvas.ForceUpdateCanvases();
            EditorApplication.QueuePlayerLoopUpdate();
            Debug.Log("SCENE2_MOTEL_ART_APPLIED: cropped native Sprite subassets; original PNGs retained; " +
                "room=motel_room; 6 inherited item slots; selected=open_suitcase.");
        }

        static void SetImage(Image image, Sprite sprite)
        {
            image.overrideSprite = null;
            image.sprite = sprite;
            image.preserveAspect = true;
            image.color = Color.white;
            image.SetAllDirty();
        }

        static ArtBounds FindBounds(BoundsFile file, string suffix)
        {
            string logicalPath = LogicalArt + suffix;
            var entry = file.assets.SingleOrDefault(asset => asset.path == logicalPath);
            Require(entry != null && entry.sprite_rect_unity != null && entry.canvas_size != null && entry.canvas_size.Length == 2,
                "Missing Sprite Rect/canvas size for " + logicalPath);
            Rect rect = entry.sprite_rect_unity.Rect;
            Require(rect.width > 0 && rect.height > 0 && rect.xMin >= 0 && rect.yMin >= 0 &&
                rect.xMax <= entry.canvas_size[0] && rect.yMax <= entry.canvas_size[1], "Out-of-range Sprite Rect: " + logicalPath);
            return entry;
        }

        static string ActualPath(ArtBounds entry) => Handoff + "/" + entry.path;

        static Sprite ImportTrimmedSprite(ArtBounds entry, SpriteDataProviderFactories factories)
        {
            string path = ActualPath(entry);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Require(importer, "Missing TextureImporter for " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 100;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.crunchedCompression = false;
            importer.maxTextureSize = Mathf.Max(2048, Mathf.NextPowerOfTwo(Mathf.Max(entry.canvas_size[0], entry.canvas_size[1])));
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);

            var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            Require(provider != null, "The installed Sprite Editor cannot provide data for " + path);
            provider.InitSpriteEditorDataProvider();
            var textureData = provider.GetDataProvider<ITextureDataProvider>();
            Require(textureData != null, "Missing source texture data for " + path);
            textureData.GetTextureActualWidthAndHeight(out int width, out int height);
            Require(width == entry.canvas_size[0] && height == entry.canvas_size[1], "Original PNG canvas differs from sprite_bounds.json: " + path);

            string name = Path.GetFileNameWithoutExtension(path);
            var previous = provider.GetSpriteRects();
            // Keep the pre-existing Sprite ID when possible, including repeated Apply calls.
            // Updating its Name/FileID pair keeps native serialized Sprite references stable.
            var old = previous.FirstOrDefault(rect => rect.name == name) ?? previous.FirstOrDefault();
            var rect = new SpriteRect
            {
                name = name,
                rect = entry.sprite_rect_unity.Rect,
                pivot = new Vector2(.5f, .5f),
                alignment = SpriteAlignment.Center,
                border = Vector4.zero,
                spriteID = old != null ? old.spriteID : GUID.Generate()
            };
            provider.SetSpriteRects(new[] { rect });
            var names = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            Require(names != null, "Missing stable Sprite Name/FileID provider for " + path);
            names.SetNameFileIdPairs(new[] { new SpriteNameFileIdPair(rect.name, rect.spriteID) });
            provider.Apply();
            importer.SaveAndReimport();

            var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
            Require(sprites.Length == 1, "Expected exactly one imported Sprite subasset for " + path);
            var sprite = sprites[0];
            Require(sprite.name == name && sprite.rect == entry.sprite_rect_unity.Rect,
                "The imported Sprite Rect does not match the supplied bounds for " + path);
            Require(EditorUtility.IsPersistent(sprite) && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string guid, out long fileId) &&
                !string.IsNullOrEmpty(guid) && fileId != 0, "Sprite is not a persistent native asset: " + path);
            return sprite;
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Scene2 motel art: " + message);
        }
    }
}
