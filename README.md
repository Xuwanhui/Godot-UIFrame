## Godot UIFrame C#版

#### 使用流程（项目中有对应示例）

1.首先将UIFrame脚本添加到全局自动加载中。

2.创建一个界面，根节点必须是Control，例如TestPanel，将该界面拖入Scene/UI/PanelLayer文件夹下（此目录目前在UIFrame脚本中写死的，具体可根据自己项目更改），同时创建一个脚本也命名为TestPanel。

3.TestPanel需要继承自UIBase或者UINode，同时用**UILayer的子类标记**该脚本，下面有脚本的介绍。

4.将TestPanel脚本挂载到TestPanel.tscn上，**切记必须同名！**

5.调用UIFrame.Show<TestPanel>();即可显示该界面。

#### 生命周期

![生命周期](/Script/UIFramework/Image/生命周期.png)

与引擎生命周期一起的顺序为：_EnterTree() => OnCreate() => _Ready() => OnRefresh()  *~(建议统一使用UI框架的生命周期)*

#### 编辑器拓展

**自动绑定功能：**当选中挂载了UIBase基类的节点时，Inspector会有一个**"Auto Bind '-' Nodes"**按钮，点击按钮就会自动绑定节点。

**规则：**脚本中[Export]的字段名需要与场景中节点名相同(忽略大小写)，场景中节点名开头用 '-' (减号)标识。

📌*详情请看TestPanel的命名方式*

#### 脚本说明

##### UIFrame.cs

```c#
// 显示UI
UIFrame.Show<TestPanel>();
UIFrame.Show<TestPanel>(new TestPanelData());
// 显示子UI
UIFrame.Show(UIBase uibase);
// 隐藏UI
UIFrame.Hide<TestPanel>();
// 隐藏子UI
UIFrame.Hide(UIbase uibase);
// 刷新UI
UIFrame.Refresh<TestPanel>();
UIFrame.Refresh<TestPanel>(new TestPanelData());
// 刷新子UI
UIFrame.Refresh(UIBase uibase);
```



##### UIBase.cs

所有UI界面的基类，UI脚本必须继承自UIBase

##### UINode

继承自UIBase，只不过继承UINode需传入该界面的UIData子类

##### UILayer

可以创建不同的Layer，需继承自UILayer，每个Layer指定层级，例如：PanelLayer,WindowLayer,FixLayer等

##### StuckUI

可以在UIFrame中设置StuckTime，当加载界面超过一定时间后，会自动打开StuckUI，具体可看项目中的TestPanel，里面模拟了耗时操作。

