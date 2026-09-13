using DeepSleep.Runtime.Progression.Upgrades;
using DeepSleep.Runtime.Combat.Beams.Presentation;
using DeepSleep.Runtime.Combat.Weapons.Harness.Presentation;
using DeepSleep.Runtime.Combat.Weapons.Harness.Melee;
using UnityEditor;
using UnityEngine;

namespace DeepSleep.Editor.Setup
{
    /// <summary>显式一次性升级配置和HS预制体；运行时只消费已装配模板。</summary>
    public static class CombatUpgradeRevisionInstaller
    {
        public static string Install()
        {
            if (EditorApplication.isPlaying) return "请先停止运行。";
            const string root = "Assets/_Project/";
            var catalog = AssetDatabase.LoadAssetAtPath<UpgradeCatalog>(root + "Configs/Progression/CFG_UpgradeCatalog_Default.asset");
            catalog.SetDefinitions(new[] {
                Card(UpgradeCardId.FaultToleranceExpansion,"生命扩容","生命上限+1，存活时补1点生命。",PlayerRoleMask.Both,5,12,6,UpgradeEffectKind.MaximumHealth,1),
                Card(UpgradeCardId.DataCompression,"饭团增幅","每颗饭团伤害+1。",PlayerRoleMask.DeepSeek,5,24,12,UpgradeEffectKind.WeaponDamage,1),
                Card(UpgradeCardId.TerminalAmplifier,"终端增幅","激光、主剑气伤害+1；每2级刀刃伤害+1。",PlayerRoleMask.Harness,5,16,8,UpgradeEffectKind.WeaponDamage,1),
                Card(UpgradeCardId.RiceFan,"饭团扇阵","弹道+1，在前方60度范围内均分，不额外复制锁定弹。",PlayerRoleMask.DeepSeek,4,24,12,UpgradeEffectKind.ProjectileCount,1),
                Card(UpgradeCardId.RiceStorm,"饭团加速","饭团攻击频率增加基础值的15%。",PlayerRoleMask.DeepSeek,4,18,9,UpgradeEffectKind.AttackRate,.15f),
                Card(UpgradeCardId.RiceGuidance,"多目标校准","各弹道在8度内校准不同目标；无目标保持原方向。",PlayerRoleMask.DeepSeek,1,24,0,UpgradeEffectKind.TargetCorrection,1),
                Card(UpgradeCardId.RiceSplash,"饭团溅射","命中时在1.2单位半径内爆炸，周围敌人受50%伤害（向下取整，最低1）；直击目标不重复受伤。",PlayerRoleMask.DeepSeek,1,36,0,UpgradeEffectKind.RiceSplash,1),
                Card(UpgradeCardId.TerminalArray,"终端阵列","发射装置+1，各束汇聚同一目标并独立贯穿。近战剑气残响+1（50%伤害）。",PlayerRoleMask.Harness,2,36,24,UpgradeEffectKind.ProjectileCount,1),
                Card(UpgradeCardId.TerminalBurst,"终端连射","每次指令多发1轮完整激光，间隔0.18秒。近战动作速度+15%。",PlayerRoleMask.Harness,2,36,24,UpgradeEffectKind.BurstCount,1),
                Card(UpgradeCardId.TerminalChain,"递归连锁","连锁层级+1，每级伤害减半，仍贯穿。近战剑与剑气范围+15%，角色不变。",PlayerRoleMask.Harness,2,40,24,UpgradeEffectKind.ChainLevel,1)
            });
            EditorUtility.SetDirty(catalog);
            var network = AssetDatabase.LoadAssetAtPath<DeepSleep.Runtime.Networking.NetworkTuningConfig>(root+"Configs/Networking/CFG_Network.asset");
            network.ProtocolVersion = 2;
            network.ContentVersion = "20260913-rice-splash-3";
            EditorUtility.SetDirty(network);
            SetFloat(root+"Configs/Combat/Harness/CFG_HA_TerminalLaser_Default.asset","_primaryTargetDamage",3f);
            foreach (string action in new[]{"Up","Down","Sweep"})
            {
                var a = AssetDatabase.LoadAssetAtPath<HarnessMeleeAttackConfig>(root+"Configs/Combat/Harness/Melee/CFG_HA_Melee_"+action+".asset");
                a.EchoAngleStep = action == "Down" ? -6f : 6f;
                a.BladeDamage=2; a.WaveDamage=3; EditorUtility.SetDirty(a);
            }
            string prefabPath=root+"Prefabs/Players/PF_Player_Harness.prefab";
            var prefab=PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var presenter=prefab.GetComponent<HarnessTerminalLaserPresenter>();
                var so=new SerializedObject(presenter);
                var lane=(BeamTiledMeshView2D)so.FindProperty("_shotView._beamLaneViews").GetArrayElementAtIndex(0).objectReferenceValue;
                var laneSo=new SerializedObject(lane);
                var layers=laneSo.FindProperty("_additionalLayers");
                if(layers.arraySize==0)
                {
                    var baseMaterial=lane.GetComponent<MeshRenderer>().sharedMaterial;
                    layers.arraySize=2;
                    string[] textures={"TEX_HA_LaserOverclock_v01","TEX_HA_LaserSurgeFrame_v01"};
                    for(int i=0;i<2;i++)
                    {
                        string materialPath=root+"Materials/Combat/Harness/MAT_"+textures[i]+".mat";
                        var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                        if(material==null)
                        { material=new Material(baseMaterial); AssetDatabase.CreateAsset(material,materialPath); }
                        material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(root+"Art/VFX/Harness/Laser/"+textures[i]+".png"));
                        EditorUtility.SetDirty(material);
                        var layer=Object.Instantiate(lane,lane.transform.parent);
                        layer.name=i==0?"Layer2_Overclock":"Layer3_SurgeFrame";
                        layer.GetComponent<MeshRenderer>().sharedMaterial=material;
                        var childSo=new SerializedObject(layer);
                        childSo.FindProperty("_additionalLayers").arraySize=0;
                        childSo.FindProperty("_orderOffset").intValue=laneSo.FindProperty("_orderOffset").intValue+i+1;
                        childSo.ApplyModifiedPropertiesWithoutUndo();
                        layers.GetArrayElementAtIndex(i).objectReferenceValue=layer;
                    }
                    laneSo.ApplyModifiedPropertiesWithoutUndo();
                    for(int i=0;i<2;i++)
                        ((BeamTiledMeshView2D)layers.GetArrayElementAtIndex(i).objectReferenceValue).transform.SetParent(lane.transform,false);
                }
                PrefabUtility.SaveAsPrefabAsset(prefab,prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            AssetDatabase.SaveAssets();
            return "10张强化卡、HS三层束体模板、基础伤害与网络协议2已更新。";
        }
        private static UpgradeDefinition Card(UpgradeCardId id,string name,string description,PlayerRoleMask roles,
            int max,int cost,int step,UpgradeEffectKind effect,float value) =>
            new(id,name,description,roles,UpgradeOwnershipScope.Role,max,cost,step,effect,value);
        private static void SetFloat(string path,string field,float value)
        {
            var asset=AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            var so=new SerializedObject(asset); var p=so.FindProperty(field);
            if(p==null) throw new System.InvalidOperationException(path+" 缺少 "+field);
            p.floatValue=value; so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(asset);
        }
    }
}
