using Godot;
namespace MySim {
// Shared MySim visual language: midnight surfaces, cool timing-line accents and warm focus.
public static class MySimTheme {
 public static readonly Color Ink=new Color(.035f,.055f,.075f),Surface=new Color(.065f,.09f,.12f),Edge=new Color(.16f,.24f,.29f),Accent=new Color(.25f,.85f,.79f),Text=new Color(.9f,.94f,.96f);
 static StyleBoxFlat Box(Color fill,Color line,int width=1){return new StyleBoxFlat {BgColor=fill,BorderColor=line,BorderWidthLeft=width,BorderWidthTop=width,BorderWidthRight=width,BorderWidthBottom=width,CornerRadiusTopLeft=6,CornerRadiusTopRight=6,CornerRadiusBottomLeft=6,CornerRadiusBottomRight=6,ContentMarginLeft=16,ContentMarginRight=16,ContentMarginTop=10,ContentMarginBottom=10};}
 public static Theme Build(){var theme=new Theme {DefaultFontSize=18};foreach(string type in new[]{"Button","OptionButton","CheckBox","Label","ItemList","SpinBox"})theme.SetColor("font_color",type,Text);
  foreach(string type in new[]{"Button","OptionButton"}){theme.SetStylebox("normal",type,Box(Surface,Edge));theme.SetStylebox("hover",type,Box(new Color(.09f,.16f,.19f),Accent));theme.SetStylebox("pressed",type,Box(new Color(.08f,.24f,.25f),Accent));theme.SetStylebox("focus",type,Box(new Color(0,0,0,0),new Color(1f,.72f,.3f),2));theme.SetStylebox("disabled",type,Box(Ink,Edge));theme.SetColor("font_disabled_color",type,new Color(.4f,.48f,.53f));}
  theme.SetStylebox("panel","PanelContainer",Box(Ink,Edge));theme.SetStylebox("panel","PopupMenu",Box(Ink,Edge));theme.SetStylebox("panel","ItemList",Box(Surface,Edge));theme.SetStylebox("selected","ItemList",Box(new Color(.08f,.24f,.25f),Accent));theme.SetStylebox("selected_focus","ItemList",Box(new Color(.08f,.24f,.25f),Accent));theme.SetColor("font_selected_color","ItemList",Text);return theme;
 }
}
}
