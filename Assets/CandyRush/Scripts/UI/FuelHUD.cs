using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CandyRush.Integrated
{
    /// <summary>Barra superior de combustível no mesmo estilo do medidor de turbo.</summary>
    public sealed class FuelHUD:MonoBehaviour
    {
        sealed class Panel
        {
            public RectTransform root;public Image fill;
            public float displayed=100,beep;public int spendSequence,warningSequence;public bool low;
            public FuelSystem fuel;
        }
        readonly Dictionary<int,Panel> panels=new Dictionary<int,Panel>();
        const float BarWidth=300,BarHeight=32;
        const float FillInset=4*BarHeight/24;
        const float FillWidth=BarWidth-2*FillInset,FillHeight=BarHeight-2*FillInset;
        static readonly Color FuelYellow=Hex.C(0xffd54f);
        Canvas canvas;GameState gameState;Sprite rounded;AudioSource audioSource;AudioClip beepClip;
        public void Initialize(GameState state,CandyPitStop station)
        {
            gameState=state;
            var go=new GameObject("Canvas Combustível",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));go.transform.SetParent(transform,false);
            canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=30;
            go.GetComponent<CanvasScaler>().uiScaleMode=CanvasScaler.ScaleMode.ConstantPixelSize;
            // Sprite técnico de cantos arredondados; a cor vem de cada Image.
            var texture=new Texture2D(64,64,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp};var pixels=new Color[4096];
            for(int y=0;y<64;y++) for(int x=0;x<64;x++)
            {float d=new Vector2(x-31.5f,y-31.5f).magnitude;pixels[y*64+x]=new Color(1,1,1,Mathf.Clamp01(32-d));}
            texture.SetPixels(pixels);texture.Apply();rounded=Sprite.Create(texture,new Rect(0,0,64,64),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(32,32,32,32));
            audioSource=gameObject.AddComponent<AudioSource>();audioSource.playOnAwake=false;audioSource.volume=.18f;
            var samples=new float[2400];for(int i=0;i<samples.Length;i++) samples[i]=Mathf.Sin(i/12000f*880*Mathf.PI*2)*Mathf.Sin(i/(float)samples.Length*Mathf.PI)*.25f;
            beepClip=AudioClip.Create("Aviso combustível",samples.Length,1,12000,false);beepClip.SetData(samples,0);
        }
        Image Image(Transform parent,string name,Rect rect,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);
            var image=go.GetComponent<Image>();image.sprite=rounded;image.type=UnityEngine.UI.Image.Type.Sliced;image.color=color;image.raycastTarget=false;
            image.pixelsPerUnitMultiplier=64/rect.height;
            Place((RectTransform)go.transform,rect);return image;
        }
        static void Place(RectTransform t,Rect r)
        {t.anchorMin=t.anchorMax=new Vector2(0,1);t.pivot=new Vector2(0,1);t.anchoredPosition=new Vector2(r.x,-r.y);t.sizeDelta=r.size;}
        Panel CreatePanel(int index)
        {
            var root=new GameObject("Gasolina jogador "+(index+1),typeof(RectTransform)).GetComponent<RectTransform>();root.SetParent(canvas.transform,false);
            var p=new Panel{root=root};
            Image(root,"Contorno do medidor",new Rect(-2,-2,BarWidth+4,BarHeight+4),GameUI.MeterOutline);
            Image(root,"Interior do medidor",new Rect(0,0,BarWidth,BarHeight),GameUI.CardNavy);
            p.fill=Image(root,"Gasolina horizontal",new Rect(FillInset,FillInset,FillWidth,FillHeight),FuelYellow);
            var labelObject=new GameObject("Nome do medidor",typeof(RectTransform),typeof(Text));labelObject.transform.SetParent(root,false);
            var label=labelObject.GetComponent<Text>();label.font=UiArt.Font!=null?UiArt.Font:Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.fontSize=16;label.fontStyle=FontStyle.Bold;
            label.text="Combustível";label.alignment=TextAnchor.MiddleCenter;label.color=Color.white;label.raycastTarget=false;
            Place(label.rectTransform,new Rect(0,0,BarWidth,BarHeight));
            var shadow=labelObject.AddComponent<Shadow>();shadow.effectColor=new Color(.12f,.15f,.27f,.85f);shadow.effectDistance=new Vector2(1,-1);
            panels.Add(index,p);return p;
        }
        public void Display(IReadOnlyList<GameUI.HudEntry> entries,float dt)
        {
            if(canvas==null) return;
            bool visible=gameState.IsRacing;
            canvas.gameObject.SetActive(visible);
            if(!visible) return;
            foreach(var panel in panels.Values) panel.root.gameObject.SetActive(false);
            foreach(var entry in entries)
            {
                if(!panels.TryGetValue(entry.Index,out var p)) p=CreatePanel(entry.Index);
                p.root.gameObject.SetActive(true);var fuel=entry.Kart.Fuel;
                if(p.fuel!=fuel || fuel.SpendSequence<p.spendSequence) {p.fuel=fuel;p.displayed=fuel.Current;p.spendSequence=fuel.SpendSequence;p.warningSequence=fuel.WarningSequence;}
                Rect safe=Screen.safeArea;
                if(safe.width<=0 || safe.height<=0) safe=new Rect(0,0,Screen.width,Screen.height);
                safe.y=Screen.height-safe.yMax;
                var vp=new Rect(entry.Viewport.X,entry.Viewport.Y,entry.Viewport.Width,entry.Viewport.Height);
                vp=Rect.MinMaxRect(Mathf.Max(vp.xMin,safe.xMin),Mathf.Max(vp.yMin,safe.yMin),Mathf.Min(vp.xMax,safe.xMax),Mathf.Min(vp.yMax,safe.yMax));
                float scale=MobileControlLayout.Create(vp).Scale;
                // Centro superior da área segura, entre os botões à esquerda e o placar à direita.
                Place(p.root,new Rect(vp.center.x-BarWidth*scale/2,vp.y+24*scale,BarWidth,BarHeight));p.root.localScale=Vector3.one*scale;
                p.displayed=Mathf.MoveTowards(p.displayed,fuel.Current,dt*70);
                float ratio=Mathf.Clamp01(p.displayed/100);
                p.fill.rectTransform.sizeDelta=new Vector2(FillWidth*ratio,FillHeight);
                p.fill.gameObject.SetActive(ratio>0);
                Color color=FuelYellow;
                if(fuel.IsLow) color.a=.5f+.5f*Mathf.Abs(Mathf.Sin(Time.unscaledTime*6));p.fill.color=color;
                p.beep-=dt;
                if(fuel.IsLow && (!p.low || p.beep<=0)) {audioSource.PlayOneShot(beepClip);p.beep=12;}p.low=fuel.IsLow;
                p.spendSequence=fuel.SpendSequence;
                if(p.warningSequence!=fuel.WarningSequence) {p.warningSequence=fuel.WarningSequence;audioSource.PlayOneShot(beepClip);}
            }
        }
        void OnDestroy() {if(rounded!=null){VisualMaterialLibrary.Dispose(rounded.texture);VisualMaterialLibrary.Dispose(rounded);}VisualMaterialLibrary.Dispose(beepClip);}
    }
}
