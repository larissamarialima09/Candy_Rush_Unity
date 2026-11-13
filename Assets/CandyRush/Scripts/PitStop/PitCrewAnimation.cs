using UnityEngine;

namespace CandyRush.Integrated
{
    /// <summary>Animações cosméticas por bomba, sem Animator obrigatório e sem alocação por quadro.</summary>
    public sealed class PitCrewAnimation:MonoBehaviour
    {
        public Transform gummyArm,marshmallowArm,tire;
        public Animator customAnimator;
        public ParticleSystem sugar;
        public AudioSource glub;
        bool working;
        float phase,blend;
        Quaternion armRest=Quaternion.identity,notesRest=Quaternion.identity,tireRest=Quaternion.identity;
        void Awake() {CapturePose();}
        void CapturePose()
        {if(gummyArm!=null) armRest=gummyArm.localRotation;if(marshmallowArm!=null) notesRest=marshmallowArm.localRotation;if(tire!=null) tireRest=tire.localRotation;}
        static AudioClip glubClip;
        Material sugarMaterial;
        public void Build()
        {
            Mat red=Mats.Candy(0xf24a88,.55f),white=Mats.Candy(0xffedda,.8f),purple=Mats.Candy(0x9562c4,.5f),black=Mats.Candy(0x30223e,.9f);
            var bear=SceneKit.Node("Ursinho de goma",transform);bear.localPosition=new Vector3(-1,0,-.2f);
            SceneKit.Mesh(bear,Geo.Sphere(.35f,10,8),red,true).At(0,.65f,0);
            SceneKit.Mesh(bear,Geo.Sphere(.27f,10,8),red,true).At(0,1.1f,0);
            foreach(float x in new[]{-.2f,.2f}) SceneKit.Mesh(bear,Geo.Sphere(.12f,8,6),red,false).At(x,1.32f,0);
            gummyArm=SceneKit.Node("Mangueira",bear);gummyArm.localPosition=new Vector3(.2f,.85f,.1f);
            SceneKit.Mesh(gummyArm,Geo.Cylinder(.055f,.055f,.7f,8),black,false).Rot(0,0,1.1f);
            var marsh=SceneKit.Node("Marshmallow prancheta",transform);marsh.localPosition=new Vector3(1.1f,0,-1);
            SceneKit.Mesh(marsh,Geo.RoundedBox(.55f,.9f,.45f,2,.18f),white,true).At(0,.65f,0);
            marshmallowArm=SceneKit.Node("Prancheta",marsh);marshmallowArm.localPosition=new Vector3(0,.65f,.3f);
            SceneKit.Mesh(marshmallowArm,Geo.Box(.38f,.45f,.04f),purple,false);
            var drop=SceneKit.Node("Gotinha roxa pneu",transform);drop.localPosition=new Vector3(1,0,1.2f);
            SceneKit.Mesh(drop,Geo.Sphere(.38f,10,8),purple,true).At(0,.65f,0);
            tire=SceneKit.Node("Pneu cosmético",drop);tire.localPosition=new Vector3(0,.45f,.4f);
            SceneKit.Mesh(tire,Geo.Torus(.24f,.09f,8,14),black,false);
            armRest=gummyArm.localRotation;notesRest=marshmallowArm.localRotation;tireRest=tire.localRotation;
            var sparkle=new GameObject("Açúcar abastecimento");sparkle.transform.SetParent(transform,false);sparkle.transform.localPosition=new Vector3(-.8f,1.3f,0);
            sugar=sparkle.AddComponent<ParticleSystem>();var main=sugar.main;
            main.playOnAwake=false;main.startLifetime=.6f;main.startSpeed=.5f;main.startSize=.07f;
            main.maxParticles=32;main.startColor=new Color(1,.75f,.9f);
            var emission=sugar.emission;emission.rateOverTime=18;
            var shader=Shader.Find("Particles/Standard Unlit");
            if(shader!=null) {sugarMaterial=new Material(shader);sugar.GetComponent<ParticleSystemRenderer>().sharedMaterial=sugarMaterial;}
            sugar.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            glub=gameObject.AddComponent<AudioSource>();glub.playOnAwake=false;glub.loop=true;glub.spatialBlend=1;glub.volume=.15f;glub.maxDistance=22;
            if(glubClip==null)
            {
                const int rate=12000;var samples=new float[rate];
                for(int i=0;i<samples.Length;i++) {float t=(float)i/rate;float p=Mathf.Repeat(t,.24f)/.24f;samples[i]=Mathf.Sin(t*Mathf.PI*2*(160+80*(1-p)))*Mathf.Sin(p*Mathf.PI)*.25f;}
                glubClip=AudioClip.Create("Glub doce",rate,1,rate,false);glubClip.SetData(samples,0);
            }
            glub.clip=glubClip;
        }
        public void SetWorking(bool value)
        {
            if(working==value) return;working=value;
            if(sugar!=null) {if(value) sugar.Play();else sugar.Stop();}
            if(glub!=null) {if(value) glub.Play();else glub.Stop();}
            if(customAnimator!=null) foreach(var p in customAnimator.parameters)
                if(p.name=="Refueling" && p.type==AnimatorControllerParameterType.Bool) customAnimator.SetBool("Refueling",value);
        }
        public void Step(float dt)
        {
            phase+=dt;blend=Mathf.MoveTowards(blend,working?1:0,dt*4);
            if(gummyArm!=null) gummyArm.localRotation=armRest*Quaternion.Euler(0,0,-45*blend);
            if(marshmallowArm!=null) marshmallowArm.localRotation=notesRest*Quaternion.Euler(Mathf.Sin(phase*12)*8*blend,0,0);
            if(tire!=null) tire.localRotation=tireRest*Quaternion.Euler(phase*120*blend,0,0);
        }
        public void SetPaused(bool value)
        {
            if(value) {if(sugar!=null) sugar.Pause();if(glub!=null) glub.Pause();}
            else if(working) {if(sugar!=null) sugar.Play();if(glub!=null) glub.UnPause();}
        }
        void OnDestroy() {VisualMaterialLibrary.Dispose(sugarMaterial);}
    }
}
