using UnityEngine;
namespace SpellDuel
{
    public static partial class TownArt
    {
        static Mesh actionBladeMesh,actionBowMesh;
        static Transform ActionHand(Transform figure,bool left)=>FacilityHand(figure,left);
        static Transform ActionProp(Transform figure,bool left,string name)=>Group(ActionHand(figure,left),name,new Vector3(0,-.025f,.04f));
        static void ActionBook(Transform figure,bool left)
        {
            var book=ActionProp(figure,left,"Resident action book");ResidentBox(book,"Leather ledger cover",Vector3.zero,new Vector3(.24f,.19f,.035f),C("80553d"));
            ResidentBox(book,"Open ledger pages",new Vector3(0,0,.025f),new Vector3(.21f,.16f,.012f),Cream);
            for(int side=-1;side<=1;side+=2)for(int i=0;i<4;i++)Box(book,"Handwritten ledger row",new Vector3(side*.055f,.055f-i*.035f,.038f),new Vector3(.075f,.005f,.004f),Ink);
            Batch(book);var page=Group(book,"Resident turning page",new Vector3(0,0,.041f));Box(page,"Ledger turning sheet",new Vector3(.05f,0,0),new Vector3(.10f,.16f,.003f),Cream);Batch(page);
        }
        static void ActionHammer(Transform figure)
        {
            var hammer=ActionProp(figure,false,"Resident action hammer");Beam(hammer,"Ash wood hammer grip",new Vector3(0,-.10f,0),new Vector3(0,.18f,0),.027f,Wood);ResidentBox(hammer,"Forged hammer head",new Vector3(0,.18f,0),new Vector3(.17f,.09f,.085f),C("9bb3bc"));Batch(hammer);
        }
        static void ActionBlade(Transform figure,bool left)
        {
            var blade=ActionProp(figure,left,"Resident inspected blade");Beam(blade,"Wrapped sword grip",new Vector3(0,-.06f,0),new Vector3(0,.06f,0),.025f,Wood);Beam(blade,"Short blade guard",new Vector3(-.07f,.06f,0),new Vector3(.07f,.06f,0),.025f,Gold);
            if(actionBladeMesh==null){var b=new OutfitArt.Builder();b.Loft(new[]{.06f,.29f,.37f},new[]{.033f,.029f,.003f},new[]{.011f,.010f,.002f},C("a6c5cd"),0,0,6);actionBladeMesh=b.Finish("Resident inspection blade");}Part(blade,"Work blade",actionBladeMesh,Vector3.zero,Vector3.one,Color.white).GetComponent<MeshRenderer>().sharedMaterial=AnimeFace.Material(Color.white,4);Batch(blade);
        }
        static void ActionAnvil(Transform figure)
        {
            var work=Group(figure,"Resident workstation",Vector3.zero);Cyl(work,"Portable anvil stump",new Vector3(.14f,.29f,.38f),new Vector3(.25f,.29f,.25f),Wood);
            ResidentBox(work,"Anvil base",new Vector3(.14f,.59f,.38f),new Vector3(.32f,.09f,.22f),Ink);ResidentBox(work,"Anvil waist",new Vector3(.14f,.67f,.38f),new Vector3(.16f,.12f,.16f),C("607987"));ResidentBox(work,"Anvil striking face",new Vector3(.14f,.75f,.38f),new Vector3(.38f,.07f,.20f),C("9bb3bc"));
            var horn=Cone(work,"Anvil horn",new Vector3(.37f,.75f,.38f),new Vector3(.12f,.22f,.12f),C("9bb3bc"));horn.transform.localRotation=Quaternion.Euler(0,0,-90);Batch(work);
            var sparks=Group(work,"Resident forge sparks",new Vector3(.14f,.80f,.38f));for(int i=0;i<5;i++){float a=i*Mathf.PI*.4f;Glow(sparks,"Forge ember",new Vector3(Mathf.Cos(a)*.10f,.03f+i*.025f,Mathf.Sin(a)*.10f),.023f,C("ffd984"));}Batch(sparks);sparks.gameObject.SetActive(false);
        }
        static void ResidentActionProps(string id,Transform figure)
        {
            if(id=="鐵匠"||id=="小鐵"||id=="鐵娘")
            {
                if(id=="小鐵")ActionHammer(figure);ActionBlade(figure,true);ActionAnvil(figure);
                if(id=="鐵娘")foreach(var tf in figure.GetComponentsInChildren<Transform>())if(tf.gameObject.name=="Innkeeper lantern"){tf.gameObject.SetActive(false);Object.Destroy(tf.gameObject);break;}
            }
            else if(id=="店員"||id=="米菈")
            {
                var tray=ActionProp(figure,true,"Resident wares tray");ResidentBox(tray,"Merchant wooden tray",new Vector3(0,0,0),new Vector3(.24f,.025f,.15f),Wood);
                for(int i=0;i<2;i++)Bottle(tray,new Vector3(-.055f+i*.11f,.02f,0),i==0?C("d87c83"):C("75bfc4"),.13f);Batch(tray);
                var shelf=Group(figure,"Resident workstation",Vector3.zero);for(int side=-1;side<=1;side+=2)Beam(shelf,"Portable wares shelf upright",new Vector3(.40f+side*.16f,.05f,.38f),new Vector3(.40f+side*.16f,1.16f,.38f),.035f,Wood);
                for(int level=0;level<2;level++){float y=.74f+level*.23f;ResidentBox(shelf,"Wares display shelf",new Vector3(.40f,y,.38f),new Vector3(.37f,.035f,.19f),Wood);for(int i=0;i<2;i++)Bottle(shelf,new Vector3(.31f+i*.17f,y+.02f,.38f),i==0?C("d87c83"):C("75bfc4"),.14f);}Batch(shelf);
                var purse=ActionProp(figure,false,"Resident counting purse");ResidentBox(purse,"Coin purse",new Vector3(0,0,0),new Vector3(.09f,.08f,.07f),Wood);for(int i=0;i<3;i++)Cyl(purse,"Counted coin",new Vector3(.045f,.03f+i*.009f,.025f),new Vector3(.04f,.004f,.04f),Gold);Batch(purse);
            }
            else if(id=="公會長"||id=="艾琳")ActionBook(figure,id=="公會長");
            else if(id=="小葉")
            {
                var tablet=figure.Find("Chest story tablet");if(tablet!=null){tablet.SetParent(ActionHand(figure,true),true);tablet.localPosition=new Vector3(.15f,0,.045f);tablet.localRotation=Quaternion.identity;}
            }
            else if(id=="楓")
            {
                // The carried lamp becomes belt equipment, freeing the bow hand.
                foreach(var tf in figure.GetComponentsInChildren<Transform>())if(tf.gameObject.name.StartsWith("ResidentToolGrip Leaf lamp")){tf.SetParent(figure,true);tf.localPosition=new Vector3(-.22f,.89f,-.12f);}
                var bow=ActionProp(figure,true,"Resident forest bow");if(actionBowMesh==null){var b=new OutfitArt.Builder();b.Tube(new[]{new Vector3(.02f,-.34f,0),new Vector3(-.075f,-.19f,.015f),new Vector3(-.095f,0,.025f),new Vector3(-.075f,.19f,.015f),new Vector3(.02f,.34f,0)},new[]{.009f,.017f,.020f,.017f,.009f},new[]{.010f,.016f,.020f,.016f,.010f},Wood,10);actionBowMesh=b.Finish("Resident forest bow");}Part(bow,"Polished yew bow",actionBowMesh,Vector3.zero,Vector3.one,Color.white).GetComponent<MeshRenderer>().sharedMaterial=AnimeFace.Material(Color.white,4);Beam(bow,"Bowstring",new Vector3(.02f,-.34f,0),new Vector3(.02f,.34f,0),.004f,Cream);Batch(bow);
            }
            else if(id=="海斗")
            {
                var scope=ActionProp(figure,false,"Resident telescope");scope.localRotation=Quaternion.Euler(90,0,0);Cyl(scope,"Telescope brass tube",new Vector3(0,.12f,0),new Vector3(.065f,.14f,.065f),Gold);Cyl(scope,"Telescope sliding sleeve",new Vector3(0,.27f,0),new Vector3(.08f,.05f,.08f),Wood);Cyl(scope,"Telescope lens",new Vector3(0,.322f,0),new Vector3(.064f,.003f,.064f),C("77bec9"));Batch(scope);
            }
            else if(id=="凪")
            {
                var lantern=FindActionPart(figure,"Innkeeper lantern");if(lantern!=null){lantern.SetParent(figure,true);lantern.localPosition=new Vector3(-.20f,1.00f,-.18f);}
                var cup=ActionProp(figure,true,"Resident tea cup");Cyl(cup,"Glazed ceramic cup",new Vector3(0,.04f,0),new Vector3(.10f,.065f,.10f),Cream);Cyl(cup,"Tea surface",new Vector3(0,.108f,0),new Vector3(.076f,.002f,.076f),C("96714e"));var handle=Group(cup,"Cup loop handle",new Vector3(.073f,.035f,0));handle.localRotation=Quaternion.Euler(90,0,0);Ring(handle,"Cup handle",.037f,.012f,0,Cream,false,14);Batch(cup);
                var cloth=ActionProp(figure,false,"Resident cleaning cloth");ResidentBox(cloth,"Folded wiping linen",new Vector3(0,-.035f,.015f),new Vector3(.11f,.11f,.013f),C("f3e9cf"));Batch(cloth);
            }
            else if(id=="阿潮")
            {
                var reel=ActionProp(figure,true,"Resident fishing reel");reel.localPosition=new Vector3(.07f,.06f,.06f);Beam(reel,"Fishing reel foot",new Vector3(-.06f,0,0),Vector3.zero,.018f,Gold);Batch(reel);
                var spool=Group(reel,"Resident spinning reel",Vector3.zero);spool.localRotation=Quaternion.Euler(90,0,0);Cyl(spool,"Brass reel spool",Vector3.zero,new Vector3(.09f,.025f,.09f),Gold);Ring(spool,"Reel rim",.052f,.009f,0,Cream,false,14);Beam(spool,"Reel crank",new Vector3(0,-.035f,0),new Vector3(.065f,-.035f,0),.012f,Wood);Ball(spool,"Reel crank knob",new Vector3(.065f,-.04f,0),Vector3.one*.028f,Wood);Batch(spool);
            }
            else if(id=="雷歐")ActionBlade(figure,false);
            else if(id=="小沙")
            {
                var ticket=ActionProp(figure,true,"Resident loose ticket");ResidentBox(ticket,"Perforated admission ticket",Vector3.zero,new Vector3(.10f,.07f,.008f),Cream);for(int i=0;i<4;i++)Box(ticket,"Ticket perforation",new Vector3(-.03f+i*.02f,0,.008f),new Vector3(.005f,.008f,.003f),Ink);Batch(ticket);
            }
            else if(id=="阿鈴"||id=="老駝")
            {
                var bells=ActionProp(figure,true,"Resident hand bells");Ring(bells,"Handheld brass ring",.055f,.011f,0,Gold,false,14);for(int i=0;i<3;i++)Part(bells,"Caravan hand bell",Shape("bell"),new Vector3(-.05f+i*.05f,-.045f,0),Vector3.one*.055f,Gold);Batch(bells);
            }
            else if(id=="懸賞官")
            {
                var board=Group(figure,"Resident workstation",Vector3.zero);for(int side=-1;side<=1;side+=2)Beam(board,"Clerk notice easel",new Vector3(side*.17f,.02f,.57f),new Vector3(side*.17f,1.46f,.57f),.045f,Wood);
                ResidentBox(board,"Clerk posting board",new Vector3(0,1.21f,.58f),new Vector3(.46f,.49f,.05f),Wood);for(int i=0;i<2;i++)Box(board,"Pinned reward sheet",new Vector3(-.12f+i*.23f,1.20f,.546f),new Vector3(.17f,.28f,.012f),Cream);Batch(board);
            }
        }
        static Transform FindActionPart(Transform root,string name){foreach(var tf in root.GetComponentsInChildren<Transform>())if(tf.gameObject.name==name)return tf;return null;}
    }
}
