using UnityEngine;
namespace SpellDuel
{
    public partial class BossRig
    {
        // Reuse the existing core child and its parent animation; the heart/shell pivots stay intact.
        void BuildStoryCrystal()
        {
            crystalM=new Material(litShader){name="Story black crystal",color=new Color(.065f,.045f,.105f)};
            var stones=core.GetComponentsInChildren<MeshFilter>();
            if(stones.Length==0)return;
            var stone=stones[0];stone.sharedMesh=ChapterArt.Crystal();stone.GetComponent<MeshRenderer>().sharedMaterial=crystalM;
            stone.gameObject.name="Story black crystal";stone.transform.localPosition=new Vector3(0,0,Theme==2?.30f:.10f);
            stone.transform.localScale=Theme==2?new Vector3(.36f,.64f,.24f):new Vector3(.32f,.48f,.27f);
            for(int i=1;i<stones.Length;i++)stones[i].gameObject.SetActive(false);
            // The glowing collar frames the crystal, retaining the original core pulse/light response.
            if(Theme!=1)MeshPart(core,ReferenceMesh("diamond core rim",b=>
            {
                var points=new[]{new Vector3(0,.5f,0),new Vector3(.5f,0,0),new Vector3(0,-.5f,0),new Vector3(-.5f,0,0),new Vector3(0,.5f,0)};
                for(int i=0;i<4;i++)b.Seam(points[i],points[i+1],.045f,Color.white);
            }),coreM,new Vector3(0,0,Theme==2?.30f:.10f),Vector3.zero,Theme==2?new Vector3(.40f,.70f,1):new Vector3(.38f,.54f,1));
            if(Theme==2)
                for(int i=0;i<3;i++)MeshPart(core,ChapterArt.MemorialName(i,false),lavaM,new Vector3((i-1)*.062f,-.01f,.424f),Vector3.zero,new Vector3(.043f,.076f,1));
            if(Theme==1&&embers!=null)
            {
                var spores=embers.main;spores.startColor=new ParticleSystem.MinMaxGradient(new Color(.60f,.20f,.95f,.8f),new Color(.85f,.55f,1f,.9f));
            }
        }
    }
}
