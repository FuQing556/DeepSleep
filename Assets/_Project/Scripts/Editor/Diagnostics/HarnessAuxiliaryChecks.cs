using System;
using System.Linq;
using System.Collections.Generic;
using DeepSleep.Runtime.Combat.Weapons.Harness;
using DeepSleep.Runtime.Combat.Weapons.Harness.Presentation;
using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Input.Commands;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.Progression.Upgrades;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace DeepSleep.Editor.Diagnostics
{
    public static class HarnessAuxiliaryChecks
    {
        public static string Run()
        {
            int count=0;
            void Check(bool value,string reason){count++;if(!value)throw new Exception(reason);}
            var bounds=new Rect(-9.6f,-5.4f,19.2f,10.8f);
            for(int n=1;n<=4;n++)for(int i=0;i<n;i++)foreach(float x in new[]{-9.6f,0,9.6f})foreach(float y in new[]{-5.4f,0,5.4f})
            {
                var p=HarnessTerminalLaserController.AuxiliaryPosition(bounds,new Vector2(x,y),n,i,.28f,.5f);
                Check(p.x>=bounds.xMin+.5f-.0001f&&p.x<=bounds.xMax-.5f+.0001f&&
                    p.y>=bounds.yMin+.5f-.0001f&&p.y<=bounds.yMax-.5f+.0001f,"Entire muzzle stays inside edge margin");
            }
            var a=HarnessTerminalLaserController.AuxiliaryPosition(bounds,Vector2.zero,4,0,.28f,.5f);
            var b=HarnessTerminalLaserController.AuxiliaryPosition(bounds,Vector2.zero,4,1,.28f,.5f);
            var c=HarnessTerminalLaserController.AuxiliaryPosition(bounds,Vector2.zero,4,2,.28f,.5f);
            Check(Mathf.Approximately(Vector2.Distance(a,b),Vector2.Distance(a,c)),"Four ports form square");
            var moved=HarnessTerminalLaserController.AuxiliaryPosition(bounds,Vector2.one,4,0,.28f,.5f);
            Check(Vector2.Distance(moved-a,Vector2.one)<.0001f,"Array follows both axes in open space");
            var edgeA=HarnessTerminalLaserController.AuxiliaryPosition(bounds,new Vector2(100,100),4,0,.28f,.5f);
            var edgeB=HarnessTerminalLaserController.AuxiliaryPosition(bounds,new Vector2(100,100),4,3,.28f,.5f);
            Check(bounds.Contains(edgeA)&&bounds.Contains(edgeB),"Individual edge clamp stays inside");
            var leftA=HarnessTerminalLaserController.AuxiliaryPosition(bounds,new Vector2(-9,0),4,0,.28f,.5f);
            var leftB=HarnessTerminalLaserController.AuxiliaryPosition(bounds,new Vector2(-9,0),4,1,.28f,.5f);
            Check(leftB.x-leftA.x<b.x-a.x,"Edge compresses array instead of moving whole square");
            var catalog=AssetDatabase.LoadAssetAtPath<UpgradeCatalog>("Assets/_Project/Configs/Progression/CFG_UpgradeCatalog_Default.asset");
            int Total(PlayerRole role)=>catalog.Definitions.Where(d=>d.Supports(role)).Sum(d=>d.MaximumRank*d.BaseTokenCost+d.MaximumRank*(d.MaximumRank-1)/2*d.AdditionalCostPerRank);
            Check(Total(PlayerRole.DeepSeek)==1340&&Total(PlayerRole.Harness)==1340,"Both totals 1340");
            catalog.TryGet(UpgradeCardId.TerminalArray,out var array);Check(array.MaximumRank==4,"Four ranks");
            foreach(string level in new[]{"Gameplay_Prototype","World01_EarlyInternet","World02_2066"})
            {
                var s=EditorSceneManager.OpenPreviewScene("Assets/Scenes/"+level+".unity");
                try {
                    var p=s.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<HarnessTerminalLaserPresenter>(true)).Single();
                    var slots=new SerializedObject(p).FindProperty("_auxiliaryMuzzles");Check(slots.arraySize==4,"Four explicit visual slots");
                    for(int i=0;i<4;i++)Check(slots.GetArrayElementAtIndex(i).objectReferenceValue!=null,"Slot survives scene reload");
                }finally{EditorSceneManager.ClosePreviewScene(s);}
            }
            return count+" auxiliary geometry/price/scene checks passed";
        }

        // 在已启动的战斗中调用：仅改当前局，不购买、不改永久存档；退出Play即丢弃。
        public static string RunRuntime()
        {
            if(!EditorApplication.isPlaying)throw new Exception("Start a combat scene first");
            var laser=UnityEngine.Object.FindFirstObjectByType<HarnessTerminalLaserController>();
            var original=laser.Upgrades.CaptureSnapshot();
            var events=new List<HarnessTerminalLaserFireRequest>();
            laser.FireRequested+=events.Add;
            try
            {
                laser.CancelSelection();laser.SetInputSuppressed(false);
                laser.Upgrades.SetRankFromAuthority(PlayerRole.Harness,UpgradeCardId.TerminalArray,4);
                var command=default(PlayerCommand);
                for(int i=0;i<80;i++)laser.ConsumeCommand(in command,.1f);
                if(events.Count==0||events.Any(e=>!e.IsAuxiliary))throw new Exception("Auto-off should produce auxiliary shots without main shots");
                int shots=events.Count;
                for(int i=0;i<30;i++)laser.ConsumeBlockedCommand(.1f);
                if(events.Count!=shots)throw new Exception("Blocked attack fired");
                for(int i=0;i<4;i++)if(!laser.GetAuxiliaryView(i,out _,out _,out var charging)||charging)throw new Exception("Blocked auxiliary should remain idle, not charge");
                return "Runtime: "+shots+" autonomous auxiliary shots with default/auto-off command; no main shot; blocked stops all four.";
            }
            finally{laser.FireRequested-=events.Add;laser.Upgrades.RestoreSnapshot(original);laser.ConsumeBlockedCommand(0);}
        }

        public static string RunRestRefresh()
        {
            if(!EditorApplication.isPlaying)throw new Exception("Play mode required");
            var laser=UnityEngine.Object.FindFirstObjectByType<HarnessTerminalLaserController>();
            var original=laser.Upgrades.CaptureSnapshot();
            int shots=0;
            void OnFire(HarnessTerminalLaserFireRequest request)=>shots++;
            laser.FireRequested+=OnFire;
            try
            {
                laser.ConsumeBlockedCommand(0);
                foreach(int rank in new[]{0,1,2,3,4,2,0})
                {
                    laser.Upgrades.SetRankFromAuthority(PlayerRole.Harness,UpgradeCardId.TerminalArray,rank);
                    // 故意不推进 ConsumeCommand，复现节点购买/菜单暂停后的纯显示刷新。
                    for(int i=0;i<4;i++)
                    {
                        bool visible=laser.GetAuxiliaryView(i,out _,out _,out var charging);
                        if(visible!=(i<rank)||charging)throw new Exception("Rest upgrade did not refresh idle ports immediately");
                    }
                }
                if(shots!=0)throw new Exception("Presentation refresh fired a weapon");
                return "Rest: 0/1/2/3/4/2/0 ranks refresh immediately without combat ticks or shots.";
            }
            finally{laser.FireRequested-=OnFire;laser.Upgrades.RestoreSnapshot(original);}
        }

        public static string RunMotion()
        {
            var laser=UnityEngine.Object.FindFirstObjectByType<HarnessTerminalLaserController>();
            var original=laser.Upgrades.CaptureSnapshot();var position=laser.transform.position;
            try
            {
                laser.Upgrades.SetRankFromAuthority(PlayerRole.Harness,UpgradeCardId.TerminalArray,4);
                laser.ConsumeBlockedCommand(0);laser.AdvanceAuxiliaryMotion(.1f);
                var ports=(Array)typeof(HarnessTerminalLaserController).GetField("_aux",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(laser);
                var p=ports.GetValue(0);var type=p.GetType();
                var start=laser.GetAuxiliaryOrigin(0);
                type.GetField("Charging").SetValue(p,true);
                laser.transform.position+=new Vector3(2,1,0);laser.AdvanceAuxiliaryMotion(.1f);
                if(laser.GetAuxiliaryOrigin(0)!=start)throw new Exception("Charging muzzle moved");
                type.GetField("Charging").SetValue(p,false);type.GetField("Hold").SetValue(p,1f);
                laser.AdvanceAuxiliaryMotion(.1f);
                if(laser.GetAuxiliaryOrigin(0)!=start)throw new Exception("Firing muzzle moved");
                type.GetField("Hold").SetValue(p,0f);laser.AdvanceAuxiliaryMotion(.01f);
                var moved=laser.GetAuxiliaryOrigin(0);
                if(moved==start||Vector2.Distance(moved,start)>1f)throw new Exception("Return should interpolate, not snap");
                return "Motion: charge/fire anchors stay fixed while HS moves; return uses gradual Lerp.";
            }
            finally{laser.transform.position=position;laser.Upgrades.RestoreSnapshot(original);laser.ConsumeBlockedCommand(0);}
        }
    }
}
