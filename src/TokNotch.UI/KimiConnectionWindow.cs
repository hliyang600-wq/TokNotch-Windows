using System.Windows;
using System.Windows.Controls;
namespace TokNotch.UI;
internal sealed class KimiConnectionWindow : Window
{
 public string ApiKey {get;private set;}="";
 public KimiConnectionWindow()
 {
  Title="连接 Kimi";Width=420;Height=210;ResizeMode=ResizeMode.NoResize;WindowStartupLocation=WindowStartupLocation.CenterScreen;
  var panel=new StackPanel{Margin=new Thickness(20)};Content=panel;
  panel.Children.Add(new TextBlock{Text="输入中国大陆 Moonshot / Kimi 平台 API key。\n仅保存在本次运行内存中，退出后清除。",Margin=new Thickness(0,0,0,14)});
  var password=new PasswordBox{Height=30};panel.Children.Add(password);
  var button=new Button{Content="连接并查询余额",Margin=new Thickness(0,14,0,0),Height=30};panel.Children.Add(button);button.Click+=(_,_)=>{if(string.IsNullOrWhiteSpace(password.Password))return;ApiKey=password.Password;password.Clear();DialogResult=true;};
 }
}

