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
    if(!labels.Any(t=>t.Contains("允许拆除火箭内墙")||t.Contains("Allow wall deconstruction"))) throw new Exception("Wall deconstruction toggle missing");
    if(!labels.Any(t=>t.Contains("输入输出组件可依附任意实心砖")||t.Contains("Mount utilities on any solid tile"))) throw new Exception("Utility mounting toggle missing");
    type.GetMethod("CloseDialog",flags).Invoke(dialog,null);
   }
   var wallPatch=asm.GetType("CustomRocketInterior.Patches.InteriorWallDeconstructionPatch");
   var applyWall=wallPatch.GetMethod("ApplyToWall",BindingFlags.Static|BindingFlags.NonPublic);
   foreach(string id in new[]{"RocketWallTile","RocketEnvelopeWindowTile","RocketInteriorGasInputPort","RocketInteriorGasOutputPort","RocketInteriorLiquidInputPort","RocketInteriorLiquidOutputPort"}) {
    if((id=="RocketWallTile"||id=="RocketEnvelopeWindowTile") && Assets.GetBuildingDef(id).ConstructionOffsetFilter!=null)
     throw new Exception("Hull still excludes working from below: "+id);
    var component=Assets.GetBuildingDef(id).BuildingComplete.GetComponent<Deconstructable>();
    bool original=component.allowDeconstruction;
    try {
     applyWall.Invoke(null,new object[]{component,true});
     bool isWall=id=="RocketWallTile"||id=="RocketEnvelopeWindowTile";
     if(component.allowDeconstruction!=isWall) throw new Exception("Wrong deconstruction permission for "+id);
     applyWall.Invoke(null,new object[]{component,false});
     if(component.allowDeconstruction) throw new Exception("Non-interior wall/port unlocked: "+id);
    } finally { component.SetAllowDeconstruction(original); }
   }
   Debug.Log("[CRI-SMOKE] Hull/window deconstruction flags verified; four fixed ports remain protected");
   var utilityPatch=asm.GetType("CustomRocketInterior.Patches.InteriorUtilityFoundationPatch");
   var solidTile=utilityPatch.GetMethod("IsBuiltSolidTile",BindingFlags.Static|BindingFlags.NonPublic);
   foreach(string id in new[]{"Tile","InsulationTile","MetalTile","GlassTile","RocketWallTile","RocketEnvelopeWindowTile","MeshTile","GasPermeableMembrane"}) {
    bool valid=(bool)solidTile.Invoke(null,new object[]{Assets.GetBuildingDef(id).BuildingComplete});
    bool expected=id!="MeshTile"&&id!="GasPermeableMembrane";
    if(valid!=expected) throw new Exception("Wrong solid support classification for "+id);
   }
   foreach(string id in new[]{"RocketInteriorGasInput","RocketInteriorGasOutput","RocketInteriorLiquidInput","RocketInteriorLiquidOutput","RocketInteriorSolidInput","RocketInteriorSolidOutput","RocketInteriorPowerPlug"})
    if(!Assets.GetBuildingDef(id).ContinuouslyCheckFoundation) throw new Exception("Support monitoring missing: "+id);
   Debug.Log("[CRI-SMOKE] Real solid-tile prefabs accepted, mesh/airflow rejected; seven utilities monitor support");
   var plug=Assets.GetBuildingDef("RocketInteriorPowerPlug").BuildingComplete;
   var support=plug.GetComponent<CustomRocketInterior.Core.InteriorPowerPlugSupport>();
   if(support==null) throw new Exception("Power plug support gate missing");
   var link=plug.GetComponent<WireUtilitySemiVirtualNetworkLink>();
   var setSupport=support.GetType().GetMethod("SetFoundationSupport",BindingFlags.Instance|BindingFlags.NonPublic);
   bool oldVisualize=link.visualizeOnly;
   try {
    setSupport.Invoke(support,new object[]{false});
    if(!link.visualizeOnly) throw new Exception("Unsupported power link not disabled");
    setSupport.Invoke(support,new object[]{true});
    if(link.visualizeOnly) throw new Exception("Supported power link not re-enabled");
   } finally { link.visualizeOnly=oldVisualize; }
   Debug.Log("[CRI-SMOKE] Power plug virtual link disconnect/reconnect gate verified");
   var resize=asm.GetType("CustomRocketInterior.Core.InteriorResizer").GetMethod("Resize",BindingFlags.Public|BindingFlags.Static);
   var template=TemplateCache.GetTemplate("expansion1::interiors/habitat_medium");
   foreach(var dims in new[]{new[]{94,94},new[]{10,10},new[]{38,38}}) {
    resize.Invoke(null,new object[]{template,dims[0],dims[1]});
    if(template.cells.Count!=dims[0]*dims[1]) throw new Exception("Real game template cell count mismatch");
    Debug.Log("[CRI-SMOKE] Real habitat template verified: "+dims[0]+"x"+dims[1]);
   }
   Debug.Log("[CRI-SMOKE] PASS: options UI, real habitat resizing, wall/port protection, built solid supports and seven utility definitions verified");
  } catch(Exception e) { Debug.LogError("[CRI-SMOKE] FAIL: "+e); }
 }
}
