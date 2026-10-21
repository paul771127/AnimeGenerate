using UnityEngine;
namespace SpellDuel
{
    public partial class BossRig
    {
        Material crystalM;
        void ConfigureChapterParticles()
        {
            var motes=embers.main;motes.startColor=Theme==4?new Color(.85f,.66f,.38f):pal.glowN;
            if(Theme==4)
            {
                motes.startSpeed=.12f;motes.gravityModifier=.6f;
                var sand=stepDust.main;sand.startColor=new Color(.80f,.61f,.36f,.55f);
            }
        }
        // Identical named pivots and rest positions to BuildGolem, with independent render pieces.
        void BuildChapterGolem()
        {
            bool bronze=Theme==3;body=Pivot("Body",transform,Vector3.zero);
            spine=Pivot("Spine",body,new Vector3(0,1.3f,0));neck=Pivot("Neck",spine,new Vector3(0,1.5f,.4f));head=Pivot("Head",neck,new Vector3(0,.1f,.12f));jaw=Pivot("Jaw",head,new Vector3(0,-.06f,.04f));
            hipL=ChapterLeg(-1,out knL,out anL);hipR=ChapterLeg(1,out knR,out anR);
            shL=ChapterArm(-1,out elL,out wrL);shR=ChapterArm(1,out elR,out wrR);
            ChapterPiece(body,new Vector3(0,1.12f,-.02f),new Vector3(1.08f,.48f,.76f),rockC,bronze);
            ChapterPiece(spine,new Vector3(0,.35f,0),new Vector3(.92f,.6f,.70f),rockC,bronze);
            ChapterPiece(spine,new Vector3(0,.96f,0),new Vector3(1.60f,.95f,1.05f),rockA,bronze);
            core=Pivot("Core",spine,new Vector3(0,.9f,.5f));
            crystalM=new Material(litShader){name="Boss black crystal",color=new Color(.075f,.055f,.14f)};
            MeshPart(core,ChapterArt.Crystal(),crystalM,new Vector3(0,0,.12f),new Vector3(0,15,12),new Vector3(.30f,.48f,.25f));
            ChapterPiece(core,new Vector3(0,0,.04f),new Vector3(.48f,.57f,.15f),coreM,true);
            plateL=ChapterPiece(spine,new Vector3(-.24f,.9f,.52f),new Vector3(.30f,.58f,.15f),rockB,bronze).transform;
            plateR=ChapterPiece(spine,new Vector3(.24f,.9f,.52f),new Vector3(.30f,.58f,.15f),rockB,bronze).transform;
            plateRestL=plateL.localPosition;plateRestR=plateR.localPosition;
            if(bronze)
            {
                // Open helmet: visor is empty space with a narrow strip of soul fire behind it.
                ChapterPiece(head,new Vector3(0,.12f,-.04f),new Vector3(.72f,.43f,.56f),rockB,true);
                Part(PrimitiveType.Cube,head,new Vector3(0,.10f,.25f),new Vector3(.47f,.045f,.035f),eyeM);
                for(int s=-1;s<=1;s+=2)
                {
                    ChapterPiece(head,new Vector3(s*.27f,-.06f,.17f),new Vector3(.17f,.33f,.24f),rockA,true);
                    ChapterPiece(spine,new Vector3(s*.91f,1.39f,-.04f),new Vector3(.84f,.40f,.86f),rockB,true);
                    for(int i=0;i<3;i++)ChapterPiece(body,new Vector3(s*(.18f+i*.14f),.91f,.28f),new Vector3(.21f,.38f,.19f),rockB,true);
                }
                // Low swept bronze crest rather than a taller head envelope.
                for(int i=0;i<5;i++)Part(PrimitiveType.Cube,head,new Vector3(0,.32f,-.20f+i*.075f),new Vector3(.085f,.15f,.08f),rockC,new Vector3(-20+i*8,0,0));
                var shield=ChapterPiece(wrL,new Vector3(-.18f,-.15f,.30f),new Vector3(1.05f,.16f,1.05f),rockB,true);shield.transform.localRotation=Quaternion.Euler(90,0,0);
                MeshPart(wrL,ChapterArt.Crystal(),rockC,new Vector3(-.18f,-.15f,.40f),Vector3.zero,new Vector3(.25f,.25f,.13f));
            }
            else
            {
                ChapterPiece(head,new Vector3(0,.08f,0),new Vector3(.76f,.50f,.62f),rockB,false);
                for(int s=-1;s<=1;s+=2)
                {
                    ChapterPiece(spine,new Vector3(s*.9f,1.30f,-.03f),new Vector3(.84f,.75f,.92f),rockB,false);
                    Part(PrimitiveType.Cube,head,new Vector3(s*.17f,.12f,.31f),new Vector3(.16f,.055f,.03f),eyeM);
                    for(int twig=0;twig<3;twig++)Part(PrimitiveType.Cylinder,spine,new Vector3(s*(.78f+twig*.07f),1.78f,-.08f+twig*.09f),new Vector3(.025f,.20f,.025f),rockC,new Vector3(0,twig*70,s*(twig*22-20)));
                    // Named memorial tablets: three crossed out, three awaiting a return.
                    for(int i=0;i<3;i++)
                    {
                        float x=s*(.38f+i*.14f),y=.55f+i*.21f;
                        Part(PrimitiveType.Cube,spine,new Vector3(x,y,.48f),new Vector3(.12f,.18f,.055f),rockB);
                        MeshPart(spine,ChapterArt.MemorialName((s<0?0:3)+i,s<0),rockC,new Vector3(x,y,.518f),Vector3.zero,new Vector3(.10f,.14f,1));
                    }
                }
                rockA.mainTexture=ChapterArt.Strata();rockB.mainTexture=ChapterArt.Strata();rockC.mainTexture=ChapterArt.Strata();
            }
        }
        GameObject ChapterPiece(Transform p,Vector3 at,Vector3 size,Material mat,bool shell)
            =>MeshPart(p,ChapterArt.Shell(shell),mat,at,Vector3.zero,size);
        Transform ChapterLeg(float side,out Transform knee,out Transform ankle)
        {
            bool shell=Theme==3;var hip=Pivot("Hip"+(side<0?"L":"R"),body,new Vector3(.42f*side,HipY,0));
            ChapterPiece(hip,new Vector3(0,-.27f,0),new Vector3(.52f,.6f,.56f),rockC,shell);
            knee=Pivot("Knee"+(side<0?"L":"R"),hip,new Vector3(0,-L1,0));ChapterPiece(knee,new Vector3(0,-.19f,.02f),new Vector3(.58f,.44f,.60f),rockB,shell);
            ankle=Pivot("Ankle"+(side<0?"L":"R"),knee,new Vector3(0,-L2,0));ChapterPiece(ankle,new Vector3(0,.01f,.16f),new Vector3(.70f,.22f,.95f),rockC,false);return hip;
        }
        Transform ChapterArm(float side,out Transform elbow,out Transform wrist)
        {
            bool shell=Theme==3;string s=side<0?"L":"R";var shoulder=Pivot("Shoulder"+s,spine,new Vector3(side,1.25f,0));
            ChapterPiece(shoulder,new Vector3(0,-.40f,0),new Vector3(.48f,.78f,.52f),rockC,shell);
            ChapterPiece(shoulder,new Vector3(0,-.08f,0),new Vector3(.34f,.14f,.34f),lavaM,true);
            elbow=Pivot("Elbow"+s,shoulder,new Vector3(0,-.8f,0));ChapterPiece(elbow,new Vector3(0,-.45f,0),new Vector3(.75f,.88f,.78f),rockA,shell);
            ChapterPiece(elbow,Vector3.zero,new Vector3(.34f,.09f,.34f),lavaM,true);
            wrist=Pivot("Wrist"+s,elbow,new Vector3(0,-.92f,0));ChapterPiece(wrist,new Vector3(0,-.25f,.03f),new Vector3(.86f,.64f,.86f),rockB,shell);return shoulder;
        }
    }
}
