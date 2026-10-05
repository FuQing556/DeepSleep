using System;
using System.Linq;
using DeepSleep.Runtime.Combat.Encounters.Doubao;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Presentation.Effects;
using DeepSleep.Runtime.Presentation.Poses;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeepSleep.Editor.Setup
{
    /// <summary>用户主动执行的圆泡修订装配，不重建关卡、角色或前景。</summary>
    public static class DoubaoRoundBubbleInstaller
    {
        const string Art = "Assets/_Project/Art/VFX/Doubao/";
        const string Characters = "Assets/_Project/Art/Characters/Doubao/";
        const string Prefabs = "Assets/_Project/Prefabs/Combat/Enemies/";
        const string Config = "Assets/_Project/Configs/Combat/Enemies/";

        [MenuItem("DeepSleep/开发/应用豆包圆字气泡与反馈")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("请退出播放模式。");
            foreach (string name in new[]{"VFX_DB_RoundBubble_v01", "VFX_DB_BubbleImpact_v01", "VFX_DB_BubbleBreak_v01"}) Import(Art+name+".png");
            foreach (string name in new[]{"SPR_DB_Lecture_v01", "SPR_DB_Dejected_v01"}) Import(Characters+name+".png");
            Bubble(Prefabs+"PF_DB_WordBubble.prefab", false);
            Bubble("Assets/_Project/Prefabs/Networking/PF_NET_DB_WordBubbleView.prefab", true);
            Boss();
            Tuning();
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/World01_EarlyInternet.unity");
            var encounter=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<DoubaoWordWallEncounter2D>(true)).Single();
            var session=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<CoopSessionController>(true)).Single();
            var impact=Effect(encounter.transform,session,"BubbleImpact",111,0.4f,1.2f,true);
            var broken=Effect(encounter.transform,session,"BubbleBreak",112,0.55f,1.05f,false);
            Set(encounter,"_impactEffects",impact); Set(encounter,"_breakEffects",broken);
            var channel=encounter.GetComponent<DoubaoEncounterNetworkChannel>();
            Set(channel,"_maximumViews",96);
            var network=AssetDatabase.LoadAssetAtPath<NetworkTuningConfig>("Assets/_Project/Configs/Networking/CFG_Network.asset");
            DeepSleep.Editor.Networking.NetworkBuildRevision.Apply(network); EditorUtility.SetDirty(network);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("[豆包圆泡] 单字圆泡、净通道、碰撞/碎裂池与战斗/离场残影已装配。");
        }

        static void Import(string path)
        {
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
            var i=(TextureImporter)AssetImporter.GetAtPath(path);
            i.textureType=TextureImporterType.Sprite; i.spriteImportMode=SpriteImportMode.Single;
            i.spritePixelsPerUnit=512; i.mipmapEnabled=false; i.alphaIsTransparency=true;
            i.maxTextureSize=2048; i.textureCompression=TextureImporterCompression.Uncompressed;
            var s=new TextureImporterSettings(); i.ReadTextureSettings(s);
            s.spriteAlignment=(int)SpriteAlignment.Center; s.spritePivot=new Vector2(.5f,.5f);
            s.spriteMeshType=SpriteMeshType.FullRect; s.spriteBorder=Vector4.zero;
            i.SetTextureSettings(s); i.SaveAndReimport();
        }

        static void Bubble(string path,bool replica)
        {
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var renderer=root.GetComponent<SpriteRenderer>();
                if(renderer!=null) UnityEngine.Object.DestroyImmediate(renderer);
                var visual=root.transform.Find("BubbleVisual");
                if(visual==null){visual=new GameObject("BubbleVisual").transform;visual.SetParent(root.transform,false);}
                renderer=visual.GetComponent<SpriteRenderer>();if(renderer==null)renderer=visual.gameObject.AddComponent<SpriteRenderer>();
                renderer.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(Art+"VFX_DB_RoundBubble_v01.png");
                renderer.drawMode=SpriteDrawMode.Simple; renderer.sortingLayerName="Gameplay";renderer.sortingOrder=34;
                visual.localScale=Vector3.one*(.84f/renderer.sprite.bounds.size.x);
                var label=root.GetComponentInChildren<TextMesh>(true);
                label.transform.localPosition=new Vector3(0,0,-.01f);label.text="最";label.characterSize=.085f;
                Component owner=replica?(Component)root.GetComponent<DoubaoWordWallReplicaView2D>():root.GetComponent<DoubaoWordWallBlock2D>();
                Set(owner,"_renderer",renderer);Set(owner,"_charactersPerLine",1);
                if(!replica)
                {
                    var old=root.GetComponent<BoxCollider2D>();if(old!=null)UnityEngine.Object.DestroyImmediate(old);
                    var circle=root.GetComponent<CircleCollider2D>();if(circle==null)circle=root.AddComponent<CircleCollider2D>();
                    circle.radius=.42f;circle.isTrigger=true;Set(owner,"_bodyCollider",circle);
                }
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }

        static void Boss()
        {
            string path=Prefabs+"PF_EN_Doubao.prefab";var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var boss=root.GetComponent<DoubaoBoss2D>(); var sr=root.GetComponent<SpriteRenderer>();
                var ghost=root.transform.Find("PoseGhost");
                if(ghost==null){ghost=new GameObject("PoseGhost").transform;ghost.SetParent(root.transform,false);}
                var gr=ghost.GetComponent<SpriteRenderer>();if(gr==null)gr=ghost.gameObject.AddComponent<SpriteRenderer>();
                gr.sortingLayerName=sr.sortingLayerName;gr.sortingOrder=sr.sortingOrder-1;gr.enabled=false;
                var pose=root.GetComponent<SpritePoseTransition2D>();if(pose==null)pose=root.AddComponent<SpritePoseTransition2D>();
                Set(pose,"_subjectRenderer",sr);Set(pose,"_ghostRenderer",gr);
                Set(pose,"_config",AssetDatabase.LoadAssetAtPath<SpritePoseTransitionConfig>("Assets/_Project/Configs/Presentation/CFG_SpritePoseTransition_Default.asset"));
                Set(boss,"_poseTransition",pose);Set(boss,"_idleSprite",sr.sprite);
                Set(boss,"_lectureSprite",AssetDatabase.LoadAssetAtPath<Sprite>(Characters+"SPR_DB_Lecture_v01.png"));
                Set(boss,"_departureSprite",AssetDatabase.LoadAssetAtPath<Sprite>(Characters+"SPR_DB_Dejected_v01.png"));
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }

        static void Tuning()
        {
            var config=AssetDatabase.LoadAssetAtPath<DoubaoWordWallConfig>(Config+"CFG_DB_WordWall_Default.asset");
            Set(config,"_referenceCharacterSize",new Vector2(2.45f,2.45f));Set(config,"_gapCharacterRange",new Vector2(1,1.5f));
            Set(config,"_groupsPerWave",4);Set(config,"_coverageWidth",19.2f);Set(config,"_blockHeight",.84f);
            Set(config,"_fallSpeed",.85f);Set(config,"_groupIntervalSeconds",1f);Set(config,"_poolCapacity",96);
            Set(config,"_horizontalJitter",0f);Set(config,"_verticalJitter",0f);
            var so=new SerializedObject(config);var p=so.FindProperty("_patterns");p.arraySize=4;
            Vector2[][] slots={new[]{new Vector2(-.43f,.86f),new Vector2(.43f,.86f),new Vector2(-.43f,0)},
                new[]{new Vector2(-.43f,.86f),new Vector2(-.43f,0),new Vector2(.43f,0)},
                new[]{new Vector2(-.43f,.86f),new Vector2(.43f,.86f),new Vector2(.43f,0)},
                new[]{new Vector2(-.43f,.86f),new Vector2(.43f,.86f),new Vector2(-.43f,0),new Vector2(.43f,0)}};
            for(int n=0;n<4;n++)
            {
                var item=p.GetArrayElementAtIndex(n);item.FindPropertyRelative("_displayName").stringValue="圆字组"+(n+1);
                var array=item.FindPropertyRelative("_slots");array.arraySize=slots[n].Length;
                for(int j=0;j<slots[n].Length;j++){var slot=array.GetArrayElementAtIndex(j);slot.FindPropertyRelative("_offset").vector2Value=slots[n][j];slot.FindPropertyRelative("_width").floatValue=.84f;}
            }
            so.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(config);
        }

        static OneShotSpriteEffectPool2D Effect(Transform parent,CoopSessionController session,string name,ushort id,float duration,float diameter,bool enemyScale)
        {
            string artName=name=="BubbleImpact"?"VFX_DB_BubbleImpact_v01":"VFX_DB_BubbleBreak_v01";
            string prefabPath=Prefabs+"PF_VFX_DB_"+name+".prefab";
            var temp=new GameObject("PF_VFX_DB_"+name);var sr=temp.AddComponent<SpriteRenderer>();
            sr.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(Art+artName+".png");sr.sortingLayerName="Gameplay";sr.sortingOrder=80;
            var effect=temp.AddComponent<OneShotSpriteEffect2D>();Set(effect,"_renderer",sr);
            if(enemyScale){Set(effect,"_effectScaleSettings",AssetDatabase.LoadAssetAtPath<CombatEffectScaleSettings>("Assets/_Project/Configs/Presentation/CFG_CombatEffectScale_Default.asset"));Set(effect,"_effectSource",(int)CombatEffectSource.Enemy);}
            var prefab=PrefabUtility.SaveAsPrefabAsset(temp,prefabPath);UnityEngine.Object.DestroyImmediate(temp);
            string configPath=Config+"CFG_DB_"+name+".asset";
            var cfg=AssetDatabase.LoadAssetAtPath<OneShotSpriteEffectConfig>(configPath);
            if(cfg==null){cfg=ScriptableObject.CreateInstance<OneShotSpriteEffectConfig>();AssetDatabase.CreateAsset(cfg,configPath);}
            Set(cfg,"_durationSeconds",duration);Set(cfg,"_worldDiameter",diameter);
            Set(cfg,"_startScaleMultiplier",.7f);Set(cfg,"_endScaleMultiplier",1.25f);Set(cfg,"_fadeStart01",.2f);
            Set(cfg,"_prewarmCount",12);Set(cfg,"_maximumCount",96);
            var t=parent.Find(name+"Effects");if(t==null){t=new GameObject(name+"Effects").transform;t.SetParent(parent,false);}
            var pool=t.GetComponent<OneShotSpriteEffectPool2D>();if(pool==null)pool=t.gameObject.AddComponent<OneShotSpriteEffectPool2D>();
            Set(pool,"_effectPrefab",prefab.GetComponent<OneShotSpriteEffect2D>());Set(pool,"_poolRoot",t);Set(pool,"_config",cfg);
            var net=t.GetComponent<NetworkEffectEventChannel>();if(net==null)net=t.gameObject.AddComponent<NetworkEffectEventChannel>();
            net.Session=session;net.Pool=pool;net.EffectId=id;EditorUtility.SetDirty(net);
            return pool;
        }

        static void Set(UnityEngine.Object obj,string name,object value)
        {
            var so=new SerializedObject(obj);var p=so.FindProperty(name);
            if(p==null)throw new InvalidOperationException(obj.name+" missing "+name);
            if(value is UnityEngine.Object u)p.objectReferenceValue=u;
            else if(value is float f)p.floatValue=f;else if(value is int n)p.intValue=n;
            else if(value is Vector2 v)p.vector2Value=v;
            so.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(obj);
        }
    }
}
