using System;
using System.Reflection;
using DeepSleep.Runtime.Combat.Weapons.Harness.Melee;
using DeepSleep.Runtime.Input.Commands;
using UnityEditor;
using UnityEngine;

namespace DeepSleep.Editor.Diagnostics
{
    public static class PlaytestMeleeInputChecks
    {
        public static string Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Run outside Play.");
            var root = PrefabUtility.LoadPrefabContents("Assets/_Project/Prefabs/Players/PF_Player_Harness.prefab");
            try
            {
                var melee = root.GetComponentInChildren<HarnessMeleeController>(true);
                var enter = Command(CommandButtonState.Pressed, 0, Vector2.left);
                melee.ConsumeCommand(in enter, .02f); melee.Simulate(.02f);
                Require(melee.IsMelee && !melee.IsSwinging, "Entering melee must not auto-swing.");
                var attack = Command(0, CommandButtonState.Pressed | CommandButtonState.Held, Vector2.left);
                melee.ConsumeCommand(in attack, .02f); melee.Simulate(.02f);
                Require(melee.IsSwinging && Mathf.Abs(Mathf.DeltaAngle(melee.AimDegrees, 180)) < .001f, "Direction aims left independent of screen/button placement.");
                uint sequence = melee.SwingSequence;
                var release = Command(0, CommandButtonState.Released, Vector2.left);
                melee.ConsumeCommand(in release, .02f);
                for (int i = 0; i < 100; i++) melee.Simulate(.02f);
                Require(!melee.IsSwinging && melee.SwingSequence == sequence, "Release finishes current slash without next slash.");
                melee.ConsumeCancellation(in enter);
                Require(!melee.IsMelee, "Exit remains valid through skill gate.");
                return "4 melee input checks passed";
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        private static PlayerCommand Command(CommandButtonState skill, CommandButtonState attack, Vector2 aim)
            => new PlayerCommand(1, 1, Vector2.zero, new AimIntent(AimReference.Direction, aim), skill, 0, attack, 0, 0, 0);
        private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    }
}
