using UnityEngine;

namespace CandyRush.Integrated
{
    public sealed partial class GameUI
    {
        FuelRaceSettings raceSettings;
        bool AutoAcceleration=>raceSettings!=null && raceSettings.autoAcceleration;
        public void SetRaceSettings(FuelRaceSettings settings) {raceSettings=settings;}
        /// <summary>Botões redondos de doce, com aro de creme e resposta ao toque.</summary>
        Rect DrawCandyControl(Rect rect,Color tint,bool pressed)
        {
            float s=Controls.Scale;
            var shadow=rect;shadow.y+=4*s;
            HudRoundRect(shadow,new Color(.22f,.1f,.32f,.3f),rect.width/2);
            HudRoundRect(rect,new Color(1,.82f,.91f,.96f),rect.width/2);
            HudRoundRect(Grow(rect,-3*s),new Color(1,.94f,.81f),rect.width/2);
            Rect face=Grow(rect,-7*s);
            HudRoundRect(face,pressed?Color.Lerp(tint,Color.white,.27f):tint,face.width/2);
            HudRoundRect(new Rect(face.x+face.width*.17f,face.y+face.height*.1f,face.width*.66f,face.height*.24f),
                new Color(1,1,1,pressed?.26f:.17f),face.height*.12f);
            return face;
        }
        void DrawControlCaption(Rect rect,string text)
        {
            float s=Controls.Scale;
            DrawLabelPill(new Rect(rect.center.x-rect.width*.54f,rect.yMax+5*s,rect.width*1.08f,21*s),text);
        }
        void DrawTurboButton()
        {
            Rect r=TurboRect;bool ready=DriftBarFull;
            if(ready) HudRoundRect(Grow(r,5*Controls.Scale),new Color(1,.5f,.8f,.28f+.12f*Mathf.Sin(Time.unscaledTime*8)),r.width/2+5*Controls.Scale);
            Rect face=DrawCandyControl(r,ready?HudPink:new Color(.55f,.48f,.65f,.92f),TouchState.TurboPressed);
            float icon=face.height*.7f;
            float cx=face.center.x,cy=face.center.y-icon*.12f;
            Color cream=ready?new Color(1,.96f,.83f):new Color(.85f,.81f,.89f);
            HudRoundRect(new Rect(cx-icon*.19f,cy-icon*.48f,icon*.38f,icon*.85f),cream,icon*.22f);
            HudRoundRect(new Rect(cx-icon*.09f,cy-icon*.19f,icon*.18f,icon*.18f),ready?HudPink:new Color(.5f,.37f,.62f),icon*.1f);
            DrawIcon(new Rect(cx-icon*.39f,cy+icon*.1f,icon*.24f,icon*.28f),UiArt.HintUp,cream);
            DrawIcon(new Rect(cx+icon*.15f,cy+icon*.1f,icon*.24f,icon*.28f),UiArt.HintUp,cream);
            DrawIcon(new Rect(cx-icon*.13f,cy+icon*.3f,icon*.26f,icon*.32f),UiArt.BoltIcon,ready?new Color(1,.79f,.24f):cream);
            DrawControlCaption(r,"TURBO");
        }
        void DrawDrivingSettings(Rect card)
        {
            if(raceSettings==null) return;
            var style=new GUIStyle(labelStyle){fontSize=18,normal={textColor=LavenderText}};
            bool automatic=GUI.Toggle(new Rect(card.x+65,card.y+330,410,32),raceSettings.autoAcceleration," ACELERAÇÃO AUTOMÁTICA: "+(raceSettings.autoAcceleration?"ON":"OFF"),style);
            bool drift=GUI.Toggle(new Rect(card.x+65,card.y+370,410,32),raceSettings.automaticDrift," DRIFT AUTOMÁTICO: "+(raceSettings.automaticDrift?"ON":"OFF"),style);
            if(automatic!=raceSettings.autoAcceleration || drift!=raceSettings.automaticDrift)
            {
                raceSettings.autoAcceleration=automatic;raceSettings.automaticDrift=drift;
                PlayerPrefs.SetInt("CandyRush.AutoAcceleration",automatic?1:0);
                PlayerPrefs.SetInt("CandyRush.AutomaticDrift",drift?1:0);PlayerPrefs.Save();
            }
        }
    }
}
