using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
public sealed class SmokeMod : KMod.UserMod2 {
 public override void OnLoad(Harmony h) { base.OnLoad(h); Debug.Log("[CRI-SMOKE] Loaded diagnostic assembly"); }
}
[HarmonyPatch(typeof(MainMenu), "OnSpawn")]
static class MainMenuSmoke {
 static bool ran;
 static void Postfix(MainMenu __instance) { if (!ran) { ran=true; __instance.StartCoroutine(Run()); } }
 static IEnumerator Run() {
  yield return new WaitForSeconds(3f);
  try {
   var asm=typeof(CustomRocketInterior.Mod).Assembly;
   var type=asm.GetType("PeterHan.PLib.Options.OptionsDialog",true);
   var flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
   Debug.Log("[CRI-SMOKE] Embedded options provider: "+type.Assembly.FullName);
   for(int i=0;i<3;i++) {
    var dialog=Activator.CreateInstance(type,flags,null,new object[]{typeof(CustomRocketInterior.Options.RocketInteriorOptions)},null);
    type.GetMethod("ShowDialog",flags).Invoke(dialog,null);
    var screen=(KScreen)type.GetField("dialog",flags).GetValue(dialog);
    if(screen==null) throw new Exception("Options screen missing");
    var labels=screen.gameObject.GetComponentsInChildren<TMPro.TMP_Text>(true).Select(t=>t.text).ToArray();
    Debug.Log("[CRI-SMOKE] UI labels: "+string.Join(" | ",labels));
    if(!labels.Any(t=>t.Contains("舱室宽度")||t.Contains("Interior Width"))) throw new Exception("Width label missing");
    if(!labels.Any(t=>t.Contains("墙体材质")||t.Contains("Wall Material"))) throw new Exception("Material combo label missing");
    type.GetMethod("CloseDialog",flags).Invoke(dialog,null);
   }
   var resize=asm.GetType("CustomRocketInterior.Core.InteriorResizer").GetMethod("Resize",BindingFlags.Public|BindingFlags.Static);
   var template=TemplateCache.GetTemplate("expansion1::interiors/habitat_medium");
   foreach(var dims in new[]{new[]{94,94},new[]{10,10},new[]{38,38}}) {
    resize.Invoke(null,new object[]{template,dims[0],dims[1]});
    if(template.cells.Count!=dims[0]*dims[1]) throw new Exception("Real game template cell count mismatch");
    Debug.Log("[CRI-SMOKE] Real habitat template verified: "+dims[0]+"x"+dims[1]);
   }
   Debug.Log("[CRI-SMOKE] PASS: options UI opened/closed 3 times and real game template resized/shrunk");
  } catch(Exception e) { Debug.LogError("[CRI-SMOKE] FAIL: "+e); }
 }
}
