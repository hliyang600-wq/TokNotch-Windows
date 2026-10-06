using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;

using TokNotch.Core.Models;
using TokNotch.Core.Interaction;
namespace TokNotch.UI;
/// <summary>Quick actions that used to live in the tray menu.</summary>
public sealed record SettingsCommands(Func<Task<string>> Refresh,Action ShowIsland,Action Exit);
public partial class SettingsWindow : Window
{
 private readonly Action<DisplayPreferences> save;
 private readonly Func<string,Task<string>> connectKimi,connectMimo;
 private readonly Func<string,Task<string>>? connectDeepSeek;
 private readonly Func<Task<string>>? forgetDeepSeek,forgetKimi;
 private readonly SettingsCommands? commands;
 private readonly Func<Task<string>>? autoMimo; private readonly Func<Task<string>>? disconnectMimo;
 private readonly Provider[] providers;
 private readonly UsageMetric[] metrics;
 private readonly Dictionary<Provider,RingChoice> rings;
 private Provider ringEditing=Provider.OpenAI;
 private bool ringUpdating;
 private sealed record RingOption(RingContent Metric,string Name){public override string ToString()=>Name;}
 private sealed record MonitorOption(string Device,string Name){public override string ToString()=>Name;}
 private readonly List<ComboBox> boxes=new();
 private bool updating;
 /// <summary>Parsed on demand so an invalid baseline never silently becomes a different number.</summary>
 public DisplayPreferences Draft
 {
  get
  {
   if(!decimal.TryParse(BaselineCny.Text.Trim(),NumberStyles.Number,CultureInfo.InvariantCulture,out var cny)||cny<=0m)throw new ArgumentException("人民币满圈金额必须是大于零的数字。");
   decimal? usd=null;var text=BaselineUsd.Text.Trim();
   if(text.Length>0){if(!decimal.TryParse(text,NumberStyles.Number,CultureInfo.InvariantCulture,out var parsed)||parsed<=0m)throw new ArgumentException("美元满圈金额必须大于零，或留空表示未设置。");usd=parsed;}
   if(!int.TryParse(RefreshSecondsBox.Text.Trim(),NumberStyles.Integer,CultureInfo.InvariantCulture,out var refresh)||refresh<DisplayPreferences.MinimumRefreshSeconds||refresh>DisplayPreferences.MaximumRefreshSeconds)throw new ArgumentException($"界面刷新间隔需在 {DisplayPreferences.MinimumRefreshSeconds}–{DisplayPreferences.MaximumRefreshSeconds} 秒之间。");
   if(!int.TryParse(BalanceSecondsBox.Text.Trim(),NumberStyles.Integer,CultureInfo.InvariantCulture,out var balance)||balance<DisplayPreferences.MinimumBalanceSeconds||balance>DisplayPreferences.MaximumBalanceSeconds)throw new ArgumentException($"余额查询间隔需在 {DisplayPreferences.MinimumBalanceSeconds}–{DisplayPreferences.MaximumBalanceSeconds} 秒之间。");
   SaveRingEditor();
   return new(providers,metrics,cny,usd,refresh,balance,SelectedExpansion,SelectedGlass,rings,ReadWindowPreferences());
  }
 }
 /// <summary>0 = hover, 1 = click, 2 = always expanded; matches the combo item order.</summary>
 internal ExpansionMode SelectedExpansion => WindowModeBox.SelectedIndex switch{1=>ExpansionMode.Click,2=>ExpansionMode.AlwaysExpanded,_=>ExpansionMode.Hover};
 internal bool SelectedGlass => MaterialBox.SelectedIndex!=1;
 public SettingsWindow(DisplayPreferences preferences,Action<DisplayPreferences> savePreferences,Func<string,Task<string>> kimi,Func<string,Task<string>> mimo,Func<Task<string>>? autoConnectMimo=null,Func<Task<string>>? disconnect=null,Func<string,Task<string>>? deepseek=null,Func<Task<string>>? forgetStoredDeepSeek=null,Func<Task<string>>? forgetStoredKimi=null,SettingsCommands? commands=null)
 {
  InitializeComponent();this.commands=commands;RefreshNowButton.IsEnabled=commands!=null;ShowIslandButton.IsEnabled=commands!=null;ExitAppButton.IsEnabled=commands!=null;connectDeepSeek=deepseek;ConnectDeepSeekButton.IsEnabled=deepseek!=null;forgetDeepSeek=forgetStoredDeepSeek;ForgetDeepSeekButton.IsEnabled=forgetDeepSeek!=null;forgetKimi=forgetStoredKimi;ForgetKimiButton.IsEnabled=forgetKimi!=null;autoMimo=autoConnectMimo;disconnectMimo=disconnect;AutoMimoButton.IsEnabled=autoMimo!=null;DisconnectMimoButton.IsEnabled=disconnectMimo!=null;save=savePreferences;connectKimi=kimi;connectMimo=mimo;providers=preferences.Providers.ToArray();metrics=preferences.Metrics.ToArray();rings=preferences.Rings.ToDictionary(pair=>pair.Key,pair=>pair.Value);
  WindowModeBox.SelectedIndex=preferences.Expansion switch{ExpansionMode.Click=>1,ExpansionMode.AlwaysExpanded=>2,_=>0};MaterialBox.SelectedIndex=preferences.GlassEnabled?0:1;
  SetGlassEditor(preferences.Window.Glass);
  ExpandedCustomFps.Text=preferences.Window.ExpandedRate.CustomFps.ToString(CultureInfo.InvariantCulture);CollapsedCustomFps.Text=preferences.Window.CollapsedRate.CustomFps.ToString(CultureInfo.InvariantCulture);
  ExpandedRateBox.SelectedIndex=(int)preferences.Window.ExpandedRate.Mode;CollapsedRateBox.SelectedIndex=(int)preferences.Window.CollapsedRate.Mode;
  ThemeBox.SelectedIndex=(int)preferences.Window.Theme;AnimationModeBox.SelectedIndex=(int)preferences.Window.Animation;CollapseDelayBox.Text=preferences.Window.CollapseDelayMilliseconds.ToString(CultureInfo.InvariantCulture);UpdatePositionEditor(preferences.Window);
  BaselineCny.Text=preferences.AmountBaselineCny.ToString("0.##",CultureInfo.InvariantCulture);BaselineUsd.Text=preferences.AmountBaselineUsd?.ToString("0.##",CultureInfo.InvariantCulture)??"";RefreshSecondsBox.Text=preferences.RefreshSeconds.ToString(CultureInfo.InvariantCulture);BalanceSecondsBox.Text=preferences.BalanceRefreshSeconds.ToString(CultureInfo.InvariantCulture);
  TextOptions.SetTextFormattingMode(this,TextFormattingMode.Display);TextOptions.SetTextRenderingMode(this,TextRenderingMode.Grayscale);
  RenderProviders();RenderMetrics();ringUpdating=true;RingProviderBox.ItemsSource=ProviderCatalog.Available;RingProviderBox.ItemTemplate=(DataTemplate)FindResource("ProviderNameTemplate");RingProviderBox.SelectedValuePath="Id";RingProviderBox.SelectedValue=ringEditing;ringUpdating=false;RenderRingEditor();UpdatePreview();ShowDisplay(this,new RoutedEventArgs());
 }
 private WindowPreferences ReadWindowPreferences()
 {
  if(!double.TryParse(EdgeOffsetBox.Text.Trim(),NumberStyles.Float,CultureInfo.InvariantCulture,out var offset)||!double.IsFinite(offset)||offset<0||offset>100)throw new ArgumentException("沿边位置需在 0–100% 之间。");
  if(!int.TryParse(EdgeMarginBox.Text.Trim(),out var margin)||margin<0||margin>100)throw new ArgumentException("边缘距离需在 0–100 之间。");
  if(!int.TryParse(CollapseDelayBox.Text.Trim(),out var delay)||delay<200||delay>1000)throw new ArgumentException("收起延迟需在 200–1000 毫秒之间。");
  var material=new GlassMaterial((RefractionMode)GlassMode.SelectedIndex,GlassDisplacement.Value,GlassBlur.Value,GlassSaturation.Value,GlassAberration.Value,GlassElasticity.Value,GlassCornerRadius.Value,GlassOverLight.IsChecked==true,GlassTintOpacity.Value);material.Validate();
  return new((AppearanceTheme)ThemeBox.SelectedIndex,(DockEdge)DockEdgeBox.SelectedIndex,(DisplayTarget)DisplayTargetBox.SelectedIndex,MonitorBox.SelectedValue as string,offset/100,margin,DragEnabledBox.IsChecked==true,(AnimationMode)AnimationModeBox.SelectedIndex,delay,ReadGlassRate(ExpandedRateBox,ExpandedCustomFps),ReadGlassRate(CollapsedRateBox,CollapsedCustomFps),material);
 }
 private void SetGlassEditor(GlassMaterial material)
 {
  GlassMode.SelectedIndex=(int)material.Mode;GlassDisplacement.Value=material.Displacement;GlassBlur.Value=material.Blur;GlassSaturation.Value=material.Saturation;GlassAberration.Value=material.Aberration;GlassElasticity.Value=material.Elasticity;GlassCornerRadius.Value=material.CornerRadius;GlassOverLight.IsChecked=material.OverLight;GlassTintOpacity.Value=material.TintOpacity;
 }
 private void ResetGlassMaterial(object sender,RoutedEventArgs args)=>SetGlassEditor(new(Displacement:65,Blur:.12,Saturation:145,Aberration:1.5,Elasticity:.2));
 private static GlassFrameRate ReadGlassRate(ComboBox box,TextBox custom)
 {
  var mode=(GlassFrameRateMode)box.SelectedIndex;var fps=30;
  if(mode==GlassFrameRateMode.Custom&&(!int.TryParse(custom.Text.Trim(),out fps)||fps<1||fps>360))throw new ArgumentException("自定义玻璃采样帧率需在 1–360 FPS 之间。");
  if(mode!=GlassFrameRateMode.Custom&&int.TryParse(custom.Text.Trim(),out var previous)&&previous>=1&&previous<=360)fps=previous;
  return new(mode,fps);
 }
 private void ChangeGlassRate(object sender,SelectionChangedEventArgs args)
 {
  if(ExpandedCustomFps is not null)ExpandedCustomFps.Visibility=ExpandedRateBox.SelectedIndex==3?Visibility.Visible:Visibility.Collapsed;
  if(CollapsedCustomFps is not null)CollapsedCustomFps.Visibility=CollapsedRateBox.SelectedIndex==3?Visibility.Visible:Visibility.Collapsed;
 }
 internal void UpdatePositionEditor(WindowPreferences preferences)
 {
  var displays=new TokNotch.Infrastructure.Windows.MonitorService().Displays;
  var choices=displays.Select((area,index)=>new MonitorOption(area.Device,$"显示器 {index+1} · {area.Right-area.Left} × {area.Bottom-area.Top}")).ToList();
  if(preferences.MonitorDevice is {} device&&!choices.Any(option=>option.Device==device))choices.Add(new(device,"未连接的显示器（暂用主屏）"));
  MonitorBox.ItemsSource=choices;MonitorBox.SelectedValuePath="Device";MonitorBox.SelectedValue=preferences.MonitorDevice??choices.FirstOrDefault()?.Device;
  DisplayTargetBox.SelectedIndex=(int)preferences.Display;DockEdgeBox.SelectedIndex=(int)preferences.Edge;EdgeOffsetBox.Text=(preferences.Offset*100).ToString("0.##",CultureInfo.InvariantCulture);EdgeMarginBox.Text=preferences.EdgeMargin.ToString(CultureInfo.InvariantCulture);DragEnabledBox.IsChecked=preferences.DragEnabled;
  MonitorBox.IsEnabled=preferences.Display==DisplayTarget.Specific;
 }
 private void ChangeDisplayTarget(object sender,SelectionChangedEventArgs args){if(MonitorBox!=null)MonitorBox.IsEnabled=DisplayTargetBox.SelectedIndex==2;}
 internal void ReportPositionSaveError(){ShowPage(PositionPage,PositionTab);SaveStatus.Text="位置已调整，但未能保存到磁盘。请检查目录权限后点击保存。";}
 private void ResetPosition(object sender,RoutedEventArgs args){UpdatePositionEditor(new());SaveStatus.Text="已恢复主屏顶部居中，保存后应用。";}
 private Button Arrow(string text,Action action,bool enabled){var button=new Button{Content=text,Style=(Style)FindResource("ActionButton"),Width=36,Height=38,Padding=new Thickness(0),Margin=new Thickness(6,0,0,0),IsEnabled=enabled};button.Click+=(_,_)=>action();return button;}
 private Grid Slot(int index){var grid=new Grid{Margin=new Thickness(0,0,0,7)};grid.ColumnDefinitions.Add(new(){Width=new GridLength(32)});grid.ColumnDefinitions.Add(new());grid.ColumnDefinitions.Add(new(){Width=GridLength.Auto});grid.Children.Add(new TextBlock{Text=(index+1).ToString("00"),FontSize=11,Foreground=new SolidColorBrush(Color.FromRgb(140,146,162)),VerticalAlignment=VerticalAlignment.Center});return grid;}
 private void RenderProviders()
 {
  ProviderSlots.Children.Clear();boxes.Clear();updating=true;
  for(int i=0;i<3;i++){int index=i;var row=Slot(i);var box=new ComboBox{ItemsSource=ProviderCatalog.Available,ItemTemplate=(DataTemplate)FindResource("ProviderNameTemplate"),SelectedValuePath="Id",SelectedValue=providers[i]};Grid.SetColumn(box,1);row.Children.Add(box);boxes.Add(box);box.SelectionChanged+=(_,_)=>{if(updating||box.SelectedValue is not Provider selected)return;var old=providers[index];int other=Array.IndexOf(providers,selected);if(other>=0)providers[other]=old;providers[index]=selected;updating=true;for(int j=0;j<boxes.Count;j++)boxes[j].SelectedValue=providers[j];updating=false;UpdatePreview();};
   var arrows=new StackPanel{Orientation=Orientation.Horizontal};arrows.Children.Add(Arrow("↑",()=>MoveProvider(index,-1),i>0));arrows.Children.Add(Arrow("↓",()=>MoveProvider(index,1),i<2));Grid.SetColumn(arrows,2);row.Children.Add(arrows);ProviderSlots.Children.Add(row);}
  updating=false;
 }
 internal void MoveProvider(int index,int delta){int next=index+delta;if(index<0||index>=3||next<0||next>=3)return;(providers[index],providers[next])=(providers[next],providers[index]);RenderProviders();UpdatePreview();}
 private void RenderMetrics(){MetricSlots.Children.Clear();for(int i=0;i<3;i++){int index=i;var row=Slot(i);var label=new Border{Background=new SolidColorBrush(Color.FromRgb(36,38,48)),CornerRadius=new(9),Padding=new(12,10,12,10),Child=new TextBlock{Text=metrics[i] switch{UsageMetric.Today=>"今日用量",UsageMetric.Month=>"本月用量",_=>"累计用量"}}};label.SetResourceReference(Border.BackgroundProperty,"ControlBackground");Grid.SetColumn(label,1);row.Children.Add(label);var arrows=new StackPanel{Orientation=Orientation.Horizontal};arrows.Children.Add(Arrow("↑",()=>MoveMetric(index,-1),i>0));arrows.Children.Add(Arrow("↓",()=>MoveMetric(index,1),i<2));Grid.SetColumn(arrows,2);row.Children.Add(arrows);MetricSlots.Children.Add(row);}}
 internal void MoveMetric(int index,int delta){int next=index+delta;if(index<0||index>=3||next<0||next>=3)return;(metrics[index],metrics[next])=(metrics[next],metrics[index]);RenderMetrics();UpdatePreview();}
 private void RenderRingEditor()
 {
  ringUpdating=true;
  var choice=rings[ringEditing];var allowed=RingChoices.Available(ringEditing).Select(metric=>new RingOption(metric,RingChoices.Name(metric))).ToArray();
  OuterRingBox.ItemsSource=allowed;OuterRingBox.SelectedValuePath="Metric";OuterRingBox.SelectedValue=choice.Outer;
  InnerRingBox.ItemsSource=new[]{new RingOption(RingContent.None,RingChoices.Name(RingContent.None))}.Concat(allowed).ToArray();InnerRingBox.SelectedValuePath="Metric";InnerRingBox.SelectedValue=choice.Inner;
  RingTokenBaselineBox.Text=choice.TokenBaseline.ToString(CultureInfo.InvariantCulture);ringUpdating=false;UpdateRingPreview();
 }
 private void SaveRingEditor()
 {
  if(OuterRingBox.SelectedValue is not RingContent outer||InnerRingBox.SelectedValue is not RingContent inner)throw new ArgumentException("请选择圆环内容。");
  if(!long.TryParse(RingTokenBaselineBox.Text.Trim(),NumberStyles.Integer,CultureInfo.InvariantCulture,out var baseline))throw new ArgumentException("Token 满圈值请输入正整数。");
  var choice=new RingChoice(outer,inner,baseline);RingChoices.Validate(ringEditing,choice);rings[ringEditing]=choice;
 }
 private void ChangeRingProvider(object sender,SelectionChangedEventArgs args)
 {
  if(ringUpdating||RingProviderBox.SelectedValue is not Provider selected||selected==ringEditing)return;
  try{SaveRingEditor();ringEditing=selected;RenderRingEditor();}
  catch(ArgumentException error){SaveStatus.Text=error.Message;ringUpdating=true;RingProviderBox.SelectedValue=ringEditing;ringUpdating=false;}
 }
 private void ChangeRingContent(object sender,SelectionChangedEventArgs args){if(ringUpdating)return;UpdateRingPreview();}
 private void ChangeRingBaseline(object sender,TextChangedEventArgs args){if(ringUpdating)return;UpdateRingPreview();}
 private void UpdateRingPreview()
 {
  if(RingPreviewText==null)return;
  var outer=OuterRingBox.SelectedValue is RingContent o?RingChoices.Name(o):"—";var inner=InnerRingBox.SelectedValue is RingContent i?RingChoices.Name(i):"—";
  RingPreviewText.Text=$"{ProviderCatalog.Name(ringEditing)} · 外圈 {outer} · 内圈 {inner}";
 }
 private void UpdatePreview()=>PreviewText.Text="窗口顺序："+string.Join("  →  ",providers.Select(ProviderCatalog.Name))+"   ·   金额圆环满圈：¥"+(decimal.TryParse(BaselineCny.Text.Trim(),NumberStyles.Number,CultureInfo.InvariantCulture,out var cny)?cny.ToString("0.##",CultureInfo.InvariantCulture):"—")+(string.IsNullOrWhiteSpace(BaselineUsd.Text)?"   ·   美元满圈未设置":"   ·   美元满圈 $"+BaselineUsd.Text.Trim())+"   ·   刷新 "+RefreshSecondsBox.Text.Trim()+"s / 余额 "+BalanceSecondsBox.Text.Trim()+"s";
 private void SavePreferences(object sender,RoutedEventArgs args)
 {
  try{var draft=Draft;save(draft);SaveStatus.Text="已保存，窗口立即更新。";}
  catch(ArgumentException error){SaveStatus.Text=error.Message;}
  catch(Exception error)when(error is IOException or UnauthorizedAccessException){SaveStatus.Text="保存失败，请检查项目目录是否可写。";}
 }
 private void ShowPage(FrameworkElement page,Button tab)
 {
  foreach(var item in new[]{DisplayPage,ConnectionPage,AppearancePage,PositionPage,AnimationPage})item.Visibility=item==page?Visibility.Visible:Visibility.Collapsed;
  foreach(var item in new[]{DisplayTab,ConnectionTab,AppearanceTab,PositionTab,AnimationTab})item.SetResourceReference(BackgroundProperty,item==tab?"SelectedBackground":"ControlBackground");
  SettingsScroll.ScrollToTop();
 }
 private void ShowDisplay(object sender,RoutedEventArgs args)=>ShowPage(DisplayPage,DisplayTab);
 internal void ShowConnectionPage()=>ShowConnections(this,new RoutedEventArgs());
 internal void ShowDisplayPage()=>ShowDisplay(this,new RoutedEventArgs());
 internal void ShowAppearancePage()=>ShowPage(AppearancePage,AppearanceTab);
 private void ShowConnections(object sender,RoutedEventArgs args)=>ShowPage(ConnectionPage,ConnectionTab);
 private void ShowAppearance(object sender,RoutedEventArgs args)=>ShowPage(AppearancePage,AppearanceTab);
 private void ShowPosition(object sender,RoutedEventArgs args)=>ShowPage(PositionPage,PositionTab);
 private void ShowAnimation(object sender,RoutedEventArgs args)=>ShowPage(AnimationPage,AnimationTab);
 private async void ConnectKimi(object sender,RoutedEventArgs args){var secret=KimiKey.Password;KimiKey.Clear();if(string.IsNullOrWhiteSpace(secret)){KimiStatus.Text="请输入 API key。";return;}ConnectKimiButton.IsEnabled=false;KimiStatus.Text="正在连接…";try{KimiStatus.Text=await connectKimi(secret);}finally{ConnectKimiButton.IsEnabled=true;}}
 private async void ConnectMimo(object sender,RoutedEventArgs args){var secret=MimoCookie.Password;MimoCookie.Clear();if(string.IsNullOrWhiteSpace(secret)){MimoStatus.Text="请粘贴控制台的完整 Cookie。";return;}ConnectMimoButton.IsEnabled=false;MimoStatus.Text="正在连接…";try{MimoStatus.Text=await connectMimo(secret);}catch(ArgumentException){MimoStatus.Text="Cookie 应为一行请求头内容。";}finally{ConnectMimoButton.IsEnabled=true;}}
 private async void ConnectDeepSeek(object sender,RoutedEventArgs args){var secret=DeepSeekKey.Password;DeepSeekKey.Clear();if(connectDeepSeek==null)return;if(string.IsNullOrWhiteSpace(secret)){DeepSeekStatus.Text="请输入 DeepSeek API key。";return;}ConnectDeepSeekButton.IsEnabled=false;DeepSeekStatus.Text="正在查询余额…";try{DeepSeekStatus.Text=await connectDeepSeek(secret);}catch(ArgumentException){DeepSeekStatus.Text="API key 格式无效。";}finally{ConnectDeepSeekButton.IsEnabled=true;}}
 private async void AutoConnectMimo(object sender,RoutedEventArgs args){if(autoMimo==null)return;AutoMimoButton.IsEnabled=false;MimoStatus.Text="请在登录页完成登录…";try{MimoStatus.Text=await autoMimo();}finally{AutoMimoButton.IsEnabled=true;}}
 private async void DisconnectMimo(object sender,RoutedEventArgs args){if(disconnectMimo==null)return;MimoStatus.Text=await disconnectMimo();}
 private async void ForgetDeepSeek(object sender,RoutedEventArgs args){if(forgetDeepSeek==null)return;ForgetDeepSeekButton.IsEnabled=false;try{DeepSeekStatus.Text=await forgetDeepSeek();}finally{ForgetDeepSeekButton.IsEnabled=true;}}
 private async void ForgetKimi(object sender,RoutedEventArgs args){if(forgetKimi==null)return;ForgetKimiButton.IsEnabled=false;try{KimiStatus.Text=await forgetKimi();}finally{ForgetKimiButton.IsEnabled=true;}}
 private async void RefreshNow(object sender,RoutedEventArgs args){if(commands==null)return;RefreshNowButton.IsEnabled=false;CommandStatus.Text="正在刷新…";try{CommandStatus.Text=await commands.Refresh();}catch(Exception error)when(error is IOException or UnauthorizedAccessException){CommandStatus.Text="刷新失败，请重试。";}finally{RefreshNowButton.IsEnabled=true;}}
 private void ShowIsland(object sender,RoutedEventArgs args){if(commands==null)return;commands.ShowIsland();CommandStatus.Text="已展开浮岛。";}
 private void ExitApp(object sender,RoutedEventArgs args)=>
  commands?.Exit();
 private void DragHeader(object sender,MouseButtonEventArgs args){if(args.LeftButton==MouseButtonState.Pressed)DragMove();}
 private void CloseSettings(object sender,RoutedEventArgs args)=>Close();
}
