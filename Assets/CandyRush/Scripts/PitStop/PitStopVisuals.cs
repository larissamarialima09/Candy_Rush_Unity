using UnityEngine;

namespace CandyRush.Integrated
{
    /// <summary>Posto independente, recuado da pista, com cobertura e loja próprias.</summary>
    public static class PitStopVisuals
    {
        static Transform Point(CandyPitStop pit,CircuitPath path,string name,float across,float along)
        {
            var t=SceneKit.Node(name,pit.transform);
            t.localPosition=path.PitLane.Point(across,along);
            t.localRotation=Quaternion.LookRotation(path.PitLane.Forward);
            return t;
        }
        public static void Build(CandyPitStop pit,CircuitPath path,FuelRaceSettings settings)
        {
            var layout=path.PitLane;
            settings.pitFraction=CircuitConfig.PitAt;
            float service=layout.ServiceInner, queueLane=service+1;
            pit.entryWaypoints=new[]{Point(pit,path,"Entrada_0",layout.Inner,-24),Point(pit,path,"Entrada_1",Mathf.Lerp(layout.Inner,queueLane,.5f),-20),Point(pit,path,"Entrada_2",queueLane,-11)};
            pit.exitWaypoints=new[]{Point(pit,path,"Saida_0",queueLane,13),Point(pit,path,"Saida_1",Mathf.Lerp(layout.Inner,queueLane,.5f),21),Point(pit,path,"Saida_2",layout.Inner,26)};
            pit.queueWaypoints=new Transform[4];
            for(int i=0;i<4;i++) pit.queueWaypoints[i]=Point(pit,path,"Fila_"+i,queueLane,-11-i*3);
            pit.pumps=new CandyPitStop.Pump[Mathf.Clamp(settings.pumpCount,1,4)];
            Mat pink=Mats.Candy(0xe84892,.35f),cream=Mats.Candy(0xfff0df,.65f),purple=Mats.Candy(0x653a7c,.5f);
            Mat metal=Mats.Standard(0xbfc7d0,new StdOptions{Roughness=.3f,Metalness=.7f});
            Mat rubber=Mats.Candy(0x292635,.92f),display=Mats.Candy(0x182c38,.28f),light=Mats.Candy(0xffefd1,.35f,.4f);
            var roadOptions=Mats.Opt(.42f);
            roadOptions.Map=pit.trackTexture!=null?pit.trackTexture:ProceduralTextures.CandyTrack();
            roadOptions.NormalMap=pit.trackNormals;roadOptions.NormalScale=.55f;roadOptions.DoubleSided=true;
            SceneKit.Mesh(pit.transform,layout.BuildPavement(),Mats.Standard(0xffffff,roadOptions),false,true,"Pista rosa do posto");
            Mat stripe=Mats.Candy(0xffe5ef,.65f);
            // Marcas no asfalto guiam entrada e saída sem bloquear a manobra livre.
            foreach(float along in new[]{-23f,-19f,-15f,14f,18f,22f})
            {
                var mark=Point(pit,path,"Guia do posto",Mathf.Lerp(layout.Inner,queueLane,.5f),along);
                SceneKit.Mesh(mark,Geo.Box(.16f,.025f,1.4f),stripe,false).At(0,-.505f,0);
            }
            for(int i=0;i<pit.pumps.Length;i++)
            {
                float across=pit.pumps.Length==1?(service+layout.Outer-2)/2:Mathf.Lerp(service+2,layout.Outer-3.5f,i/(float)(pit.pumps.Length-1));
                var pump=new CandyPitStop.Pump{
                    approach=Point(pit,path,"Bomba_"+i+"_Aproximacao",across,-6),
                    stop=Point(pit,path,"Bomba_"+i+"_Parada",across,0),
                    clear=Point(pit,path,"Bomba_"+i+"_Liberacao",across,8)};
                pit.pumps[i]=pump;
                var unit=SceneKit.Node("BombaDoce_"+i,pit.transform);
                unit.localPosition=layout.Point(across+1.35f,0,0);
                unit.localRotation=Quaternion.LookRotation(-layout.Outward);
                SceneKit.Mesh(unit,Geo.RoundedBox(1.22f,.18f,2.5f,2,.09f),cream,true,true,"Ilha da bomba").At(0,.09f,0);
                SceneKit.Mesh(unit,Geo.RoundedBox(.78f,.8f,.64f,2,.09f),pink,true,true,"Base esmaltada").At(0,.58f,0);
                SceneKit.Mesh(unit,Geo.RoundedBox(.94f,.95f,.72f,2,.12f),cream,true,true,"Gabinete").At(0,1.45f,0);
                SceneKit.Mesh(unit,Geo.RoundedBox(.67f,.4f,.035f,2,.035f),display,false,true,"Visor digital").At(0,1.59f,.38f);
                SceneKit.Mesh(unit,Geo.Box(.65f,.07f,.04f),pink,false).At(0,1.18f,.38f);
                for(int digit=0;digit<3;digit++)
                    foreach(float y in new[]{1.47f,1.59f,1.71f})
                        SceneKit.Mesh(unit,Geo.Box(.105f,.022f,.012f),light,false).At(-.19f+digit*.19f,y,.405f);
                SceneKit.Mesh(unit,Geo.Tube(new[]{new Vector3(.49f,1.7f,0),new Vector3(.74f,1.4f,0),new Vector3(.78f,.45f,.06f),new Vector3(.52f,.5f,.28f),new Vector3(.49f,1.12f,.3f)},20,.045f,8),rubber,false,true,"Mangueira flexível");
                SceneKit.Mesh(unit,Geo.Box(.09f,.29f,.09f),metal,false,true,"Bico metálico").At(.5f,1.2f,.32f).Rot(0,0,-.3f);
                SceneKit.Mesh(unit,Geo.RoundedBox(.9f,.2f,.76f,2,.07f),pink,true).At(0,1.99f,0);
                foreach(float z in new[]{-.95f,.95f})
                {
                    SceneKit.Mesh(unit,Geo.Cylinder(.07f,.09f,.65f,10),metal,true,true,"Balizador").At(0,.48f,z);
                    SceneKit.Mesh(unit,Geo.Cylinder(.078f,.078f,.12f,10),pink,false).At(0,.7f,z);
                }
                pump.crew=unit.gameObject.AddComponent<PitCrewAnimation>();pump.crew.Build();
                // Contorno da vaga e número da bomba, pintados no piso.
                foreach(int side in new[]{-1,1})
                {
                    var line=Point(pit,path,"Vaga "+(i+1),across+side*1.05f,0);
                    SceneKit.Mesh(line,Geo.Box(.10f,.02f,4.8f),stripe,false).At(0,-.51f,0);
                }
            }
            // Toda a cobertura fica pelo menos dez metros além da borda da pista.
            var canopy=SceneKit.Node("Marquise independente",pit.transform);
            canopy.localPosition=layout.Point((service+layout.Outer)/2,0,0);
            canopy.localRotation=Quaternion.LookRotation(layout.Forward);
            float width=layout.Outer-service;
            SceneKit.Mesh(canopy,Geo.RoundedBox(width,.38f,25,2,.16f),pink,true,true,"Testeira rosa").At(0,4.5f,0);
            SceneKit.Mesh(canopy,Geo.RoundedBox(width+.18f,.16f,25.18f,2,.07f),cream,true,true,"Cobertura creme").At(0,4.77f,0);
            SceneKit.Mesh(canopy,Geo.Box(width-.25f,.08f,24.6f),cream,false,true,"Forro").At(0,4.27f,0);
            foreach(float x in new[]{-width*.5f+.4f,width*.5f-.4f}) foreach(float z in new[]{-11.8f,11.8f})
            {
                SceneKit.Mesh(canopy,Geo.Cylinder(.13f,.16f,4.25f,12),metal,true,true,"Coluna").At(x,2.13f,z);
                SceneKit.Mesh(canopy,Geo.RoundedBox(.46f,.5f,.46f,1,.06f),purple,true).At(x,.25f,z);
            }
            foreach(float z in new[]{-8f,0f,8f})
                SceneKit.Mesh(canopy,Geo.RoundedBox(width*.65f,.035f,.24f,1,.015f),light,false,true,"Luminária embutida").At(0,4.20f,z);
            AddSign(pit,layout.Point(service-.12f,0,4.52f),-layout.Outward,"POSTO DOCE",6.8f,purple);
            var totem=Point(pit,path,"Totem de entrada",service-1,-23);
            SceneKit.Mesh(totem,Geo.Box(.18f,2.8f,.18f),metal,true).At(0,.9f,0);
            AddSign(pit,layout.Point(service-1,-23,2.9f),-layout.Outward,"POSTO  >",3.3f,pink);
            BuildStore(pit,layout,cream,pink,purple,metal);
            Mat tireMaterial=Mats.Candy(0x34263d,.92f);
            for(int i=0;i<3;i++) SceneKit.Mesh(canopy,Geo.Torus(.42f,.14f,8,16),tireMaterial,false).At(width*.5f-.7f,.2f+i*.3f,9).Rot(Mathf.PI*.5f,0,0);
            // Sem BoxCollider sob escala espelhada: a entrada usa a mesma área lógica do piso.
        }
        static void BuildStore(CandyPitStop pit,PitLaneLayout layout,Mat cream,Mat pink,Mat purple,Mat metal)
        {
            // O prédio antigo com cúpula deixa de ser gerado. Fachada nova voltada às bombas.
            float depth=CircuitConfig.PitDepth,length=24f,height=3.8f;
            var store=SceneKit.Node("Loja de conveniência do posto",pit.transform);
            store.localPosition=layout.Point(layout.Outer+depth/2+.6f,0,0);
            store.localRotation=Quaternion.LookRotation(-layout.Outward);
            SceneKit.Mesh(store,Geo.RoundedBox(length+.8f,.22f,depth+1.2f,2,.10f),cream,true,true,"Calçada").At(0,.08f,0);
            SceneKit.Mesh(store,Geo.RoundedBox(length,height,depth,2,.12f),cream,true,true,"Edifício do posto").At(0,height/2,0);
            SceneKit.Mesh(store,Geo.RoundedBox(length+.6f,.24f,depth+.6f,2,.1f),purple,true,true,"Telhado plano").At(0,height+.12f,0);
            SceneKit.Mesh(store,Geo.Box(length,.36f,.16f),pink,true,true,"Faixa da fachada").At(0,3.2f,depth/2+.07f);
            Mat glass=Mats.Candy(0x426577,.2f);
            foreach(float x in new[]{-8f,-4.5f,4.5f,8f})
            {
                SceneKit.Mesh(store,Geo.Box(2.95f,2.15f,.12f),purple,false,true,"Caixilho").At(x,1.63f,depth/2+.08f);
                SceneKit.Mesh(store,Geo.Box(2.73f,1.93f,.06f),glass,false,true,"Vidraça").At(x,1.63f,depth/2+.16f);
                SceneKit.Mesh(store,Geo.Box(.05f,1.93f,.07f),metal,false).At(x,1.63f,depth/2+.20f);
                SceneKit.Mesh(store,Geo.Box(2.4f,.045f,.025f),cream,false,true,"Reflexo no vidro").At(x,2.24f,depth/2+.20f);
            }
            SceneKit.Mesh(store,Geo.Box(2.1f,2.7f,.15f),purple,false,true,"Entrada").At(0,1.42f,depth/2+.09f);
            SceneKit.Mesh(store,Geo.Box(1.85f,2.45f,.05f),glass,false).At(0,1.42f,depth/2+.19f);
            foreach(float x in new[]{-.20f,.20f}) SceneKit.Mesh(store,Geo.Box(.045f,.5f,.08f),metal,false,true,"Puxador").At(x,1.4f,depth/2+.25f);
            SceneKit.Mesh(store,Geo.RoundedBox(4.2f,.13f,1.4f,2,.06f),pink,true,true,"Marquise da porta").At(0,2.94f,depth/2+.55f);
            AddSign(pit,layout.Point(layout.Outer+.37f,0,3.35f),-layout.Outward,"DOCE PARADA",4.5f,purple);
            Mat green=Mats.Candy(0x78a581,.88f);
            foreach(float x in new[]{-10.8f,10.8f})
            {
                SceneKit.Mesh(store,Geo.RoundedBox(1.1f,.55f,.8f,2,.1f),purple,true,true,"Floreira").At(x,.4f,depth/2+.55f);
                SceneKit.Mesh(store,Geo.Sphere(.48f,12,8),green,true,true,"Arbusto").At(x,.9f,depth/2+.55f).Scl(1,.8f,.7f);
            }
        }
        static void AddSign(CandyPitStop pit,Vector3 position,Vector3 facing,string text,float width,Mat material)
        {
            var sign=SceneKit.Node(text,pit.transform);sign.localPosition=position;sign.localRotation=Quaternion.LookRotation(facing);
            SceneKit.Mesh(sign,Geo.RoundedBox(width,.85f,.18f,1,.12f),material,false);
            var options=Mats.Opt(.65f);options.Map=ProceduralTextures.Sign(text,"#a53176");options.DoubleSided=true;
            SceneKit.Mesh(sign,Geo.Plane(width-.12f,.73f),Mats.Standard(0xffffff,options),false).At(0,0,.11f);
        }
    }
}
