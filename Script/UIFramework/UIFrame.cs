using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using HappySquad.Script.Runtime;
using ControlTask = System.Threading.Tasks.Task<Godot.Control>;
using UIBaseTask = System.Threading.Tasks.Task<UIFramework.UIBase>;
using Task = System.Threading.Tasks.Task;

namespace UIFramework;

public partial class UIFrame : Node
{
    private static readonly Dictionary<Type, Control> controls = new Dictionary<Type, Control>();
    private static readonly Stack<(Type type, UIData data)> panelStack = new Stack<(Type, UIData)>();
    private static readonly Dictionary<UILayer, CanvasLayer> uiLayers = new Dictionary<UILayer, CanvasLayer>();
    private static CanvasLayer uiLayer;

    /// <summary>
    /// 当加载UI超过这个时间（单位：秒）时，检测为卡住
    /// </summary>
    private static readonly float StuckTime = 0.5f;

    /// <summary>
    /// 当前显示的Panel
    /// </summary>
    public static UIBase CurrentPanel
    {
        get
        {
            if (panelStack.Count <= 0) return null;

            if (panelStack.Peek().type == null) return null;

            if (controls.TryGetValue(panelStack.Peek().type, out var control))
            {
                return control as UIBase;
            }
            return null;
        }
    }

    public override void _EnterTree()
    {
        base._EnterTree();
        Init();
    }

    private async void Init()
    {
        CanvasLayer canvasLayer = new CanvasLayer();
        canvasLayer.Name = "UILayer";
        CanvasLayer stuckLayer = new CanvasLayer();
        stuckLayer.Name = "StuckLayer";
        AddChild(canvasLayer);
        AddChild(stuckLayer);
        uiLayer = canvasLayer;
        PackedScene load = ResourceLoader.Load<PackedScene>("res://Script/UIFramework/StuckUI/StuckPanel.tscn");
        Control stuckPanel = load.Instantiate<Control>();
        stuckLayer.AddChild(stuckPanel);
        stuckPanel.Visible = false;
        OnStuckStart += () =>
        {
            stuckPanel.Visible = true;
            stuckPanel.SetProcess(true);
        };
        OnStuckEnd += () =>
        {
            stuckPanel.Visible = false;
            stuckPanel.SetProcess(false);
        };
        LoadNodeFunc += LoadNode;
        await UIFrame.Show<TestPanel>(new TestPanelData()
        {
            TestString = "初始化"
        });
    }

    private ControlTask LoadNode(Type type)
    {
        UILayer layer = GetLayer(type);
        PackedScene load = ResourceLoader.Load<PackedScene>($"res://Scene/UI/{layer.GetName()}/{type.Name}.tscn");
        return Task.FromResult(load.Instantiate<Control>());
    }

    #region 事件
    /// <summary>
    /// 卡住开始时触发的事件
    /// </summary>
    public static event Action OnStuckStart;

    /// <summary>
    /// 卡住结束时触发的事件
    /// </summary>
    public static event Action OnStuckEnd;

    /// <summary>
    /// 资源请求
    /// </summary>
    public static event Func<Type, ControlTask> LoadNodeFunc;

    /// <summary>
    /// 资源释放
    /// </summary>
    public static event Action<Type> OnNodeRelease;

    /// <summary>
    /// UI创建时调用
    /// </summary>
    public static event Action<UIBase> OnCreate;

    /// <summary>
    /// UI刷新时调用
    /// </summary>
    public static event Action<UIBase> OnRefresh;

    /// <summary>
    /// UI绑定事件时调用
    /// </summary>
    public static event Action<UIBase> OnBind;

    /// <summary>
    /// UI解绑事件时调用
    /// </summary>
    public static event Action<UIBase> OnUnbind;

    /// <summary>
    /// UI显示时调用
    /// </summary>
    public static event Action<UIBase> OnShow;

    /// <summary>
    /// UI隐藏时调用
    /// </summary>
    public static event Action<UIBase> OnHide;

    /// <summary>
    /// UI销毁时调用
    /// </summary>
    public static event Action<UIBase> OnDied;
    #endregion

    #region 显示
    /// <summary>
    /// 显示UI
    /// </summary>
    public static UIBaseTask Show(UIBase ui, UIData data = null)
    {
        if (GetLayer(ui) != null && ui.Parent != null) throw new Exception("子UI不能使用UILayer属性");

        return ShowAsync(ui, data);
    }

    /// <summary>
    /// 显示Panel或Window
    /// </summary>
    public static Task<T> Show<T>(UIData data = null) where T : UIBase
    {
        return ShowAsync<T>(data);
    }

    /// <summary>
    /// 显示Panel或Window
    /// </summary>
    public static UIBaseTask Show(Type type, UIData data = null)
    {
        if (GetLayer(type) == null) throw new Exception("请使用[UILayer]子类标记！");

        return ShowAsync(type, data);
    }
    #endregion

    #region 隐藏
    /// <summary>
    /// 隐藏Panel
    /// </summary>
    public static Task Hide(bool forceDestroy = false)
    {
        return HideAsync(forceDestroy);
    }

    /// <summary>
    /// 隐藏Panel或Window
    /// </summary>
    public static Task Hide<T>(bool forceDestroy = false)
    {
        return Hide(typeof(T), forceDestroy);
    }

    /// <summary>
    /// 隐藏Panel或Window
    /// </summary>
    public static Task Hide(Type type, bool forceDestroy = false)
    {
        if (GetLayer(type) is PanelLayer)
        {
            if (CurrentPanel != null && CurrentPanel.GetType() == type) return Hide();

            throw new Exception(type + "当前Panel未显示！");
        }
        else if (GetLayer(type) != null)
        {
            if (controls.TryGetValue(type, out var control))
            {
                var uiBase = control as UIBase;
                var uiBases = uiBase.BreadthTraversal().ToArray();
                DoUnbind(uiBases);
                DoHide(uiBases);
                control.Visible = false;
                if (uiBase.AutoDestroy || forceDestroy) ReleaseControl(type);
            }
            return Task.CompletedTask;
        }
        throw new Exception("隐藏UI失败，请使用[UILayer]子类标记！");
    }

    /// <summary>
    /// 隐藏UI，forceDestroy对子UI无效。
    /// </summary>
    public static Task Hide(UIBase ui, bool forceDestroy = false)
    {
        if (GetLayer(ui) == null)
        {
            if (!ui.Visible) return Task.CompletedTask;

            var uiBases = ui.BreadthTraversal().ToArray();
            DoUnbind(uiBases);
            DoHide(uiBases);
            ui.Visible = false;
            return Task.CompletedTask;
        }
        return Hide(ui.GetType(), forceDestroy);
    }
    #endregion

    #region 获得
    /// <summary>
    /// 获得已经实例化的UI
    /// </summary>
    public static UIBase Get(Type type)
    {
        if (controls.TryGetValue(type, out var control))
        {
            return control as UIBase;
        }
        return null;
    }

    /// <summary>
    /// 获得已经实例化的UI
    /// </summary>
    public static T Get<T>() where T : UIBase
    {
        return Get(typeof(T)) as T;
    }

    /// <summary>
    /// 获得已经实例化的UI
    /// </summary>
    public static bool TryGet<T>(out T ui) where T : UIBase
    {
        ui = Get<T>();
        return ui != null;
    }

    /// <summary>
    /// 获得已经实例化的UI
    /// </summary>
    public static bool TryGet(Type type, out UIBase ui)
    {
        ui = Get(type);
        return ui != null;
    }

    /// <summary>
    /// 获得所有已经实例化的UI
    /// </summary>
    public static IEnumerable<UIBase> GetAll(Func<Type, bool> predicate = null)
    {
        foreach (var item in controls)
        {
            if (predicate != null && !predicate.Invoke(item.Key)) continue;

            yield return item.Value as UIBase;
        }
    }

    /// <summary>
    /// 获得UILayer
    /// </summary>
    public static UILayer GetLayer(Type type)
    {
        if (type == null) return null;

        var layer = type.GetCustomAttributes(typeof(UILayer), true).FirstOrDefault() as UILayer;
        return layer;
    }

    /// <summary>
    /// 获得UILayer
    /// </summary>
    public static UILayer GetLayer(UIBase ui)
    {
        return GetLayer(ui.GetType());
    }

    /// <summary>
    /// 获得UI层RectTransform
    /// </summary>
    public static CanvasLayer GetLayerCanvasLayer(Type type)
    {
        var layer = GetLayer(type);
        uiLayers.TryGetValue(layer, out var result);
        return result;
    }
    #endregion

    #region 刷新
    /// <summary>
    /// 刷新UI。data为null时，将用之前的data刷新
    /// </summary>
    public static Task Refresh<T>(UIData data = null)
    {
        return Refresh(typeof(T), data);
    }

    /// <summary>
    /// 刷新UI。data为null时将用之前的data刷新
    /// </summary>
    public static Task Refresh(Type type, UIData data = null)
    {
        if (type != null && controls.TryGetValue(type, out var control))
        {
            return Refresh(control as UIBase, data);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// 刷新UI。data为null时将用之前的data刷新
    /// </summary>
    public static Task Refresh(UIBase ui, UIData data = null)
    {
        if (!ui.Visible) return Task.CompletedTask;

        var uiBases = ui.BreadthTraversal().ToArray();
        if (data != null) TrySetData(ui, data);
        if (panelStack.Count > 0 && GetLayer(ui) is PanelLayer)
        {
            var (type, _) = panelStack.Pop();
            panelStack.Push((type, data));
        }
        return DoRefresh(uiBases);
    }

    /// <summary>
    /// 刷新所有UI
    /// </summary>
    public static async Task RefreshAll(Func<Type, bool> predicate = null)
    {
        foreach (var item in controls)
        {
            if (predicate != null && !predicate.Invoke(item.Key)) continue;

            await Refresh(item.Value as UIBase);
        }
    }
    #endregion

    /// <summary>
    /// 创建UI
    /// </summary>
    public static ControlTask Instantiate(Control control, CanvasLayer layer = null, UIData data = null)
    {
        return InstantiateAsync(control, layer, data);
    }

    /// <summary>
    /// 销毁UI
    /// </summary>
    public static void QueueFree(Control control)
    {
        UIBase uiBase = control as UIBase;
        var uiBases = uiBase.BreadthTraversal()
            .OfType<UIBase>()
            .ToArray();
        var parentUI = GetParent(uiBases.FirstOrDefault());
        foreach (var item in uiBases)
        {
            if (parentUI == null) break;

            if (GetParent(item) != parentUI) break;

            parentUI.Children.Remove(item);
        }
        foreach (var item in uiBases)
        {
            try
            {
                OnDied?.Invoke(item);
                item.InnerOnDied();
            }
            catch (Exception ex)
            {
                GD.PrintErr(ex);
            }
        }
        control.QueueFree();
    }

    /// <summary>
    /// 立即销毁UI
    /// </summary>
    public static void QueueFreeImmediate(Control control)
    {
        UIBase uiBase = control as UIBase;
        var uiBases = uiBase.BreadthTraversal()
            .OfType<UIBase>()
            .ToArray();
        var parentUI = GetParent(uiBases.FirstOrDefault());
        foreach (var item in uiBases)
        {
            if (parentUI == null) break;

            if (GetParent(item) != parentUI) break;

            parentUI.Children.Remove(item);
        }
        foreach (var item in uiBases)
        {
            try
            {
                OnDied?.Invoke(item);
                item.InnerOnDied();
            }
            catch (Exception ex)
            {
                GD.PrintErr(ex);
            }
        }
        control.Free();
    }

    /// <summary>
    /// 强制释放已经关闭的UI，即使UI的AutoDestroy为false，仍然释放该资源
    /// </summary>
    public static void ReleaseAllUnVisible()
    {
        var keys = new List<Type>();
        foreach (var item in controls)
        {
            if (item.Value is { Visible: false })
            {
                UIFrame.QueueFree(item.Value);
                OnNodeRelease?.Invoke(item.Key);
                keys.Add(item.Key);
            }
        }
        foreach (var item in keys)
        {
            controls.Remove(item);
        }
    }

    private static async ControlTask LoadControl(Type type, UIData data)
    {
        if (type == null) throw new NullReferenceException();

        if (controls.TryGetValue(type, out var control))
        {
            TrySetData(control as UIBase, data);
            return control;
        }
        Control refControl = null;
        if (LoadNodeFunc != null)
        {
            refControl = await LoadNodeFunc.Invoke(type);
        }
        else
        {
            GD.PrintErr("请设置LoadNodeFunc事件");
            return null;
        }
        var uiBase = refControl as UIBase;
        if (uiBase == null) throw new Exception("Control节点没有挂载继承自UIBase的脚本");
        var layer = GetOrCreateCanvasLayer(type);
        control = await Instantiate(refControl, layer, data);
        controls[type] = control;
        return control;
    }

    private static void ReleaseControl(Type type)
    {
        if (type == null) return;

        if (controls.TryGetValue(type, out var control))
        {
            control.QueueFree();
            OnNodeRelease?.Invoke(type);
            controls.Remove(type);
        }
    }

    private static async Task DoRefresh(IList<UIBase> uiBases)
    {
        if (uiBases == null) return;

        for (int i = 0; i < uiBases.Count; ++i)
        {
            if (uiBases[i] == null) continue;

            if (i == 0 || uiBases[i].Visible)
            {
                try
                {
                    OnRefresh?.Invoke(uiBases[i]);
                    await uiBases[i].InnerOnRefresh();
                }
                catch (Exception ex)
                {
                    GD.PrintErr(ex);
                }
            }
        }
    }

    private static void DoBind(IList<UIBase> uiBases)
    {
        if (uiBases == null) return;

        for (int i = 0; i < uiBases.Count; ++i)
        {
            if (uiBases[i] == null) continue;

            if (i == 0 || uiBases[i].Visible)
            {
                try
                {
                    OnBind?.Invoke(uiBases[i]);
                    uiBases[i].InnerOnBind();

                }
                catch (Exception ex)
                {
                    GD.PrintErr(ex);
                }
            }
        }
    }

    private static void DoUnbind(IList<UIBase> uiBases)
    {
        if (uiBases == null) return;

        for (int i = uiBases.Count - 1; i >= 0; --i)
        {
            if (uiBases[i] == null) continue;

            if (i == 0 || uiBases[i].Visible)
            {
                try
                {
                    OnUnbind?.Invoke(uiBases[i]);
                    uiBases[i].InnerOnUnbind();
                }
                catch (Exception ex)
                {
                    GD.PrintErr(ex);
                }
            }
        }
    }
    

    private static void DoShow(IList<UIBase> uiBases)
    {
        if (uiBases == null) return;

        for (int i = 0; i < uiBases.Count; ++i)
        {
            if (uiBases[i] == null) continue;

            if (i == 0 || uiBases[i].Visible)
            {
                try
                {
                    OnShow?.Invoke(uiBases[i]);
                    uiBases[i].InnerOnShow();
                }
                catch (Exception ex)
                {
                    GD.PrintErr(ex);
                }
            }
        }
    }

    private static void DoHide(IList<UIBase> uiBases)
    {
        if (uiBases == null) return;

        for (int i = uiBases.Count - 1; i >= 0; --i)
        {
            if (uiBases[i] == null) continue;

            if (i == 0 || uiBases[i].Visible)
            {
                try
                {
                    OnHide?.Invoke(uiBases[i]);
                    uiBases[i].InnerOnHide();
                }
                catch (Exception ex)
                {
                    GD.PrintErr(ex);
                }
            }
        }
    }

    private static async ControlTask InstantiateAsync(Control scene, CanvasLayer layer, UIData data)
    {
        scene.Visible = false;
        layer.AddChild(scene);
        var uiBase = scene as UIBase;
        var uiBases = uiBase
            .BreadthTraversal()
            .OfType<UIBase>()
            .ToArray();
        TrySetData(uiBase, data);
        foreach (var item in uiBases)
        {
            item.Children.Clear();
        }
        foreach (var item in uiBases)
        {
            var parentUI = GetParent(item);
            if (parentUI == null) continue;
            parentUI.Children.Add(item);
            item.Parent = parentUI;
        }
        foreach (var item in uiBases)
        {
            try
            {
                OnCreate?.Invoke(item);
                await item.InnerOnCreate();
            }
            catch (Exception ex)
            {
                GD.PrintErr(ex);
            }
        }
        if (GetLayer(uiBase) == null)
        {
            await DoRefresh(uiBases);
            scene.Visible = true;
            DoBind(uiBases);
            DoShow(uiBases);
        }
        return scene;
    }

    private static async UIBaseTask ShowAsync(UIBase ui, UIData data = null)
    {
        try
        {
            if (GetLayer(ui) == null)
            {
                if (ui.Visible) return ui;

                TrySetData(ui, data);
                var timeout = new CancellationTokenSource();
                bool isStuck = false;
                Task.Delay(TimeSpan.FromSeconds(StuckTime), timeout.Token).GetAwaiter().OnCompleted(() =>
                {
                    if (timeout.IsCancellationRequested) return;

                    OnStuckStart?.Invoke();
                    isStuck = true;
                });
                var parentUIBases = ui.Parent.BreadthTraversal().ToArray();
                DoUnbind(parentUIBases);
                var uiBases = ui.BreadthTraversal().ToArray();
                await DoRefresh(uiBases);
                ui.Visible = true;
                if (ui.Parent != null)
                {
                    DoBind(parentUIBases);
                }
                else
                {
                    DoBind(uiBases);
                }
                DoShow(uiBases);
                await timeout.CancelAsync();

                if (isStuck) OnStuckEnd?.Invoke();

                return ui;
            }
            return await Show(ui.GetType(), data);
        }
        catch (Exception ex)
        {
            GD.PrintErr(ex);
            return null;
        }
    }
    
    private static async Task<T> ShowAsync<T>(UIData data = null) where T : UIBase
    {
        var result = await Show(typeof(T), data);
        return result as T;
    }

    private static async UIBaseTask ShowAsync(Type type, UIData data = null)
    {
        try
        {
            var timeout = new CancellationTokenSource();
            UIBase result = null;
            bool isStuck = false;
            Task.Delay(TimeSpan.FromSeconds(StuckTime), timeout.Token).GetAwaiter().OnCompleted(() =>
            {
                if (timeout.IsCancellationRequested) return;
                OnStuckStart?.Invoke();
                isStuck = true;
            });
            if (GetLayer(type) is PanelLayer)
            {
                if (CurrentPanel != null && type == CurrentPanel.GetType()) return CurrentPanel;

                UIBase[] currentUIBases = null;
                if (CurrentPanel != null)
                {
                    currentUIBases = CurrentPanel.BreadthTraversal().ToArray();
                    DoUnbind(currentUIBases);
                }
                var control = await LoadControl(type, data);
                UIBase uiBase = control as UIBase;
                var uiBases = uiBase.BreadthTraversal().ToArray();
                if (data != null && CurrentPanel != null)
                {
                    data.Sender = CurrentPanel.GetType();
                }
                await DoRefresh(uiBases);
                if (CurrentPanel != null)
                {
                    DoHide(currentUIBases);
                    CurrentPanel.Visible = false;
                    if (CurrentPanel.AutoDestroy) ReleaseControl(CurrentPanel.GetType());
                }
                control.Visible = true;
                panelStack.Push((type, data));
                DoBind(uiBases);
                DoShow(uiBases);
                result = uiBase;
            }
            else if (GetLayer(type) != null)
            {
                var control = await LoadControl(type, data);
                UIBase uiBase = control as UIBase;
                var uiBases = uiBase.BreadthTraversal().ToArray();

                if (data != null && CurrentPanel != null)
                {
                    data.Sender = CurrentPanel.GetType();
                }
                await DoRefresh(uiBases);
                control.Visible = true;
                control.GetParent().MoveChild(control, control.GetParent().GetChildCount() - 1);
                DoBind(uiBases);
                DoShow(uiBases);
                result = uiBase;
            }
            await timeout.CancelAsync();
            if (isStuck) OnStuckEnd?.Invoke();
            return result;
        }
        catch (Exception ex)
        {
            GD.PrintErr(ex);
            return null;
        }
    }

    private static async Task HideAsync(bool forceDestroy)
    {
        try
        {
            var timeout = new CancellationTokenSource();

            bool isStuck = false;
            Task.Delay(TimeSpan.FromSeconds(StuckTime), timeout.Token).GetAwaiter().OnCompleted(() =>
            {
                if (timeout.IsCancellationRequested) return;
                OnStuckStart?.Invoke();
                isStuck = true;
            });

            if (CurrentPanel == null)
            {
                await timeout.CancelAsync();
                return;
            }

            var currentPanel = CurrentPanel;
            var currentUIBases = currentPanel.BreadthTraversal().ToArray();
            panelStack.Pop();
            DoUnbind(currentUIBases);
            if (panelStack.Count > 0)
            {
                var data = panelStack.Peek().data;
                if (data != null && currentPanel != null)
                {
                    data.Sender = currentPanel.GetType();
                }
                var control = await LoadControl(panelStack.Peek().type, data);
                UIBase uiBase = control as UIBase;
                var uiBases = uiBase.BreadthTraversal().ToArray();
                await DoRefresh(uiBases);
                currentPanel.Visible = false;
                DoHide(currentUIBases);
                if (currentPanel.AutoDestroy || forceDestroy) ReleaseControl(currentPanel.GetType());
                control.Visible = true;
                control.GetParent().MoveChild(control, control.GetParent().GetChildCount() - 1);
                DoBind(uiBases);
                DoShow(uiBases);
            }
            else
            {
                currentPanel.Visible = false;
                DoHide(currentUIBases);
                if (currentPanel.AutoDestroy || forceDestroy) ReleaseControl(currentPanel.GetType());
            }
            await timeout.CancelAsync();
            if (isStuck) OnStuckEnd?.Invoke();
        }
        catch (Exception ex)
        {
            GD.PrintErr(ex);
        }
    }

    private static bool TrySetData(UIBase ui, UIData data)
    {
        if (ui == null) return false;
        var property = ui.GetType().GetProperty("Data", BindingFlags.Public | BindingFlags.Instance);
        if (property == null) return false;
        property.SetValue(ui, data);
        return true;
    }

    private static UIBase GetParent(UIBase ui)
    {
        if (ui == null) return null;

        Node parent = ui.GetParent();
        while (parent != null)
        {
            if (parent is UIBase)
            {
                return parent as UIBase;
            }
            parent = parent.GetParent();
        }
        return null;
    }

    private static CanvasLayer GetOrCreateCanvasLayer(Type type)
    {
        UILayer layer = GetLayer(type);
        if (!uiLayers.TryGetValue(layer, out var canvasLayer))
        {
            canvasLayer = new CanvasLayer();
            canvasLayer.Name = layer.GetName();
            uiLayer.AddChild(canvasLayer);
            uiLayers[layer] = canvasLayer;
            int index = 0;
            foreach (var item in uiLayers.OrderBy(i => i.Key.GetOrder()))
            {
                uiLayer.MoveChild(item.Value,++index);
            }
        }
        return canvasLayer;
    }
    
}