using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Rune.Avalonia;

/// <summary>一种格式的视频源（vue-tsx-pro 的 VideoSource）：内核从上到下取第一个能播的。</summary>
/// <param name="Src">地址。</param>
/// <param name="Type">格式（"video/webm"、"video/mp4; codecs=…"）：内核不认的跳过。</param>
/// <param name="Media">媒体条件（"(max-width: 40rem)"）：窄的时候换一个小的文件。</param>
public sealed record VideoSource(string Src, string? Type = null, string? Media = null);

/// <summary>一条文本轨道（vue-tsx-pro 的 VideoTrack）：字幕、说明、章节。</summary>
/// <param name="Src">WebVTT 文件的地址。</param>
/// <param name="SrcLang">语言（BCP 47："zh"、"en"）。</param>
/// <param name="Label">菜单里显示的名字。</param>
/// <param name="Kind">种类：subtitles（默认，对白的翻译）、captions（还包括音效说明）、descriptions、chapters、metadata。</param>
/// <param name="Default">默认打开这一条。</param>
public sealed record VideoTrack(string Src, string SrcLang, string Label, string Kind = "subtitles", bool Default = false);

/// <summary>Video 的控制条（vue-tsx-pro 的 controls：true、'custom'、false）。</summary>
public enum VideoControlBar
{
    /// <summary>网页内核自己的控制条。</summary>
    Native,

    /// <summary>自绘的控制条：跟主题，带倍速、字幕菜单与全屏。</summary>
    Custom,

    /// <summary>不要控制条（自动播放的装饰视频）；减少动态效果时仍显示内核的控制条。</summary>
    None,
}

/// <summary>自绘控制条的外观与文案：颜色是 CSS 颜色（取自主题），字号是像素。</summary>
/// <param name="Lang">语言包（video* 文案）。</param>
/// <param name="FontSize">字号（px）：按钮、菜单按它算。</param>
/// <param name="Speeds">倍速菜单里的选项。</param>
/// <param name="OnScrim">深色画面上的文字与图标色。</param>
/// <param name="ScrimStrong">控制条下面渐变的深色。</param>
/// <param name="Surface">菜单的底色（surfaceRaised）。</param>
/// <param name="Text">菜单的文字色。</param>
/// <param name="Border">菜单的描边色。</param>
/// <param name="FillHover">菜单项悬停的底色。</param>
/// <param name="FocusRing">焦点环的颜色。</param>
public sealed record VideoChrome(Lang Lang, double FontSize, IReadOnlyList<double> Speeds,
    string OnScrim, string ScrimStrong, string Surface, string Text, string Border, string FillHover, string FocusRing)
{
    /// <summary>倍速菜单的默认选项。</summary>
    public static readonly IReadOnlyList<double> DefaultSpeeds = [0.5, 0.75, 1, 1.25, 1.5, 2];

    /// <summary>ARGB → CSS 的 rgba()。</summary>
    public static string Css(byte a, byte r, byte g, byte b) =>
        string.Create(CultureInfo.InvariantCulture, $"rgba({r},{g},{b},{a / 255d:0.###})");
}

/// <summary>
/// Video 放进网页内核里的页面：铺满、黑底、按 contain 放画面的 video 元素（vue-tsx-pro 的 Video 渲染的那个元素），
/// 自绘控制条时还有控制条（同上游的 controls="custom"：播放 / 暂停、时间、进度、静音与音量、字幕、倍速、全屏，几秒不动收起，键盘快捷键）。
/// </summary>
public static class VideoPage
{
    /// <summary>页面的 HTML。</summary>
    /// <param name="src">视频地址；有 sources 时不用。</param>
    /// <param name="sources">几种格式的视频源。</param>
    /// <param name="poster">封面。</param>
    /// <param name="tracks">文本轨道。</param>
    /// <param name="controls">控制条（调用方已按"减少动态效果"把不自动播放时的 None 换成 Native）。</param>
    /// <param name="autoplay">自动播放（调用方已按"减少动态效果"关掉）：同时静音、行内播放。</param>
    /// <param name="loop">循环。</param>
    /// <param name="muted">静音。</param>
    /// <param name="label">读屏念的名称（video 的 aria-label）。</param>
    /// <param name="chrome">自绘控制条的外观与文案；Custom 时必填。</param>
    public static string Html(string? src, IReadOnlyList<VideoSource>? sources, string? poster, IReadOnlyList<VideoTrack>? tracks,
        VideoControlBar controls, bool autoplay, bool loop, bool muted, string? label, VideoChrome? chrome = null)
    {
        var custom = controls == VideoControlBar.Custom;
        if (custom && chrome is null) throw new ArgumentNullException(nameof(chrome), "自绘控制条要给外观与文案");
        var html = new StringBuilder();
        html.Append("<!doctype html><html><head><meta charset=\"utf-8\"><style>")
            .Append("html,body{margin:0;height:100%;background:#000;overflow:hidden}")
            .Append("video{display:block;width:100%;height:100%;object-fit:contain;background:#000}");
        if (custom) html.Append(CustomStyle(chrome!));
        html.Append("</style></head><body>");
        if (custom) html.Append("<div id=\"frame\" tabindex=\"-1\">");
        html.Append("<video id=\"media\" preload=\"metadata\" playsinline");
        if (sources is not { Count: > 0 } && src is not null) Attribute(html, "src", src);
        if (poster is not null) Attribute(html, "poster", poster);
        if (label is not null) Attribute(html, "aria-label", label);
        if (controls == VideoControlBar.Native) html.Append(" controls");
        // 自动播放要静音才被内核允许
        if (autoplay) html.Append(" autoplay muted");
        else if (muted) html.Append(" muted");
        if (loop) html.Append(" loop");
        html.Append('>');
        foreach (var source in sources ?? [])
        {
            html.Append("<source");
            Attribute(html, "src", source.Src);
            if (source.Type is not null) Attribute(html, "type", source.Type);
            if (source.Media is not null) Attribute(html, "media", source.Media);
            html.Append('>');
        }
        foreach (var track in tracks ?? [])
        {
            html.Append("<track");
            Attribute(html, "kind", track.Kind);
            Attribute(html, "src", track.Src);
            Attribute(html, "srclang", track.SrcLang);
            Attribute(html, "label", track.Label);
            if (track.Default) html.Append(" default");
            html.Append('>');
        }
        html.Append("</video>");
        if (custom) html.Append(CustomBar(chrome!, tracks is { Count: > 0 }));
        return html.Append("</body></html>").ToString();
    }

    static void Attribute(StringBuilder html, string name, string value) =>
        html.Append(' ').Append(name).Append("=\"").Append(WebUtility.HtmlEncode(value)).Append('"');

    static string Icon(IconData icon) =>
        $"<svg viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"{icon.Path}\"/></svg>";

    /// <summary>自绘控制条的样式（上游 videoStyles）：贴底边、下面压一层渐变，白色图标按钮，滑块是半透明的白轨道。</summary>
    static string CustomStyle(VideoChrome chrome) => string.Create(CultureInfo.InvariantCulture, $$"""
        :root{--on:{{chrome.OnScrim}};--scrim:{{chrome.ScrimStrong}};--surface:{{chrome.Surface}};--text:{{chrome.Text}};--border:{{chrome.Border}};--hover:{{chrome.FillHover}};--ring:{{chrome.FocusRing}};font:{{chrome.FontSize}}px system-ui,"Segoe UI","Microsoft YaHei UI",sans-serif}
        #frame{position:relative;height:100%;outline:none}
        #frame.idle{cursor:none}
        .bar{position:absolute;left:0;right:0;bottom:0;display:flex;align-items:center;gap:.25em;padding:.25em .5em;color:var(--on);background:linear-gradient(to top,var(--scrim),transparent);transition:opacity .2s cubic-bezier(.2,0,0,1)}
        .bar.hidden:not(:has(:focus-visible)){opacity:0;pointer-events:none}
        button{flex-shrink:0;display:inline-grid;place-items:center;width:2em;height:2em;padding:0;border:0;border-radius:.4em;background:transparent;color:inherit;cursor:pointer;font:inherit}
        button:hover{background:color-mix(in srgb,var(--on) 16%,transparent)}
        button svg{width:1.15em;height:1.15em}
        :focus-visible{outline:2px solid var(--ring);outline-offset:2px}
        .time{flex-shrink:0;font-size:.75em;font-variant-numeric:tabular-nums;white-space:nowrap}
        input[type=range]{-webkit-appearance:none;appearance:none;margin:0;height:1.25em;background:transparent;cursor:pointer;--p:0%}
        input[type=range]:disabled{cursor:default;opacity:.5}
        input[type=range]::-webkit-slider-runnable-track{height:4px;border-radius:2px;background:linear-gradient(to right,var(--on) var(--p),color-mix(in srgb,var(--on) 30%,transparent) var(--p))}
        input[type=range]::-webkit-slider-thumb{-webkit-appearance:none;width:12px;height:12px;margin-top:-4px;border-radius:50%;background:var(--on)}
        #seek{flex-grow:1;min-width:4em}
        #volume{width:4.5em;flex-shrink:0}
        @media (pointer:coarse){#volume{display:none} }
        .menu{position:relative}
        [role=menu]{position:absolute;right:0;bottom:calc(100% + 6px);min-width:8em;padding:.25em;border:1px solid var(--border);border-radius:.75em;background:var(--surface);color:var(--text);box-shadow:0 10px 15px -3px rgba(0,0,0,.2),0 4px 6px -4px rgba(0,0,0,.2);font-size:.875em}
        [role=menu][hidden]{display:none}
        [role=menuitemradio]{display:flex;align-items:center;gap:.5em;padding:.375em .75em .375em .5em;border-radius:.35em;cursor:pointer;white-space:nowrap;outline:none}
        [role=menuitemradio]:hover,[role=menuitemradio]:focus{background:var(--hover)}
        [role=menuitemradio] svg{width:1em;height:1em;visibility:hidden}
        [role=menuitemradio][aria-checked=true] svg{visibility:visible}
        """);

    /// <summary>自绘控制条的元素与脚本（上游 Video 的 renderControls 与事件）：挂上就按 video 现在的状态同步一次（错过 loadedmetadata 时长也对）。</summary>
    static string CustomBar(VideoChrome chrome, bool captions)
    {
        var lang = chrome.Lang;
        var config = JsonSerializer.Serialize(new
        {
            play = lang.VideoPlay, pause = lang.VideoPause, mute = lang.VideoMute, unmute = lang.VideoUnmute,
            // 插值文案换成带占位的模板，脚本里再填
            time = lang.VideoTime("\u0001", "\u0002"),
            fullscreen = lang.VideoFullscreen, exitFullscreen = lang.VideoExitFullscreen,
            icons = new
            {
                play = Icon(Lucide.Play), pause = Icon(Lucide.Pause), volume = Icon(Lucide.Volume2), muted = Icon(Lucide.VolumeX),
                maximize = Icon(Lucide.Maximize), minimize = Icon(Lucide.Minimize),
            },
        });
        string Encode(string text) => WebUtility.HtmlEncode(text);
        string Item(string value, string label) => $"<div role=\"menuitemradio\" tabindex=\"-1\" data-value=\"{Encode(value)}\" aria-checked=\"false\">{Icon(Lucide.Check)}{Encode(label)}</div>";
        var bar = new StringBuilder("<div class=\"bar\" id=\"bar\">");
        bar.Append($"<button id=\"play\" aria-label=\"{Encode(lang.VideoPlay)}\">{Icon(Lucide.Play)}</button>")
            .Append("<span class=\"time\" id=\"time\">0:00 / 0:00</span>")
            .Append($"<input id=\"seek\" type=\"range\" min=\"0\" max=\"1\" step=\"1\" value=\"0\" disabled aria-label=\"{Encode(lang.VideoSeek)}\">")
            .Append($"<button id=\"mute\" aria-label=\"{Encode(lang.VideoMute)}\" aria-pressed=\"false\">{Icon(Lucide.Volume2)}</button>")
            .Append($"<input id=\"volume\" type=\"range\" min=\"0\" max=\"1\" step=\"0.05\" value=\"1\" aria-label=\"{Encode(lang.VideoVolume)}\">");
        if (captions)
        {
            bar.Append($"<div class=\"menu\"><button id=\"captions\" aria-haspopup=\"menu\" aria-expanded=\"false\" aria-label=\"{Encode(lang.VideoCaptions)}\">{Icon(Lucide.Captions)}</button>")
                .Append($"<div role=\"menu\" hidden aria-label=\"{Encode(lang.VideoCaptions)}\" data-kind=\"captions\">{Item("", lang.VideoCaptionsOff)}</div></div>");
        }
        bar.Append($"<div class=\"menu\"><button id=\"speed\" aria-haspopup=\"menu\" aria-expanded=\"false\" aria-label=\"{Encode(lang.VideoSpeed)}\">{Icon(Lucide.Gauge)}</button>")
            .Append($"<div role=\"menu\" hidden aria-label=\"{Encode(lang.VideoSpeed)}\" data-kind=\"speed\">");
        foreach (var speed in chrome.Speeds)
        {
            var value = speed.ToString(CultureInfo.InvariantCulture);
            bar.Append(Item(value, $"{value}×"));
        }
        bar.Append("</div></div>")
            .Append($"<button id=\"fullscreen\" aria-label=\"{Encode(lang.VideoFullscreen)}\">{Icon(Lucide.Maximize)}</button>")
            .Append("</div></div><script>const C=").Append(config).Append(';').Append(Script).Append("</script>");
        return bar.ToString();
    }

    /// <summary>控制条的行为（同上游）：播放中鼠标离开或 2.5 秒不动收起，菜单开着、鼠标停在控制条上、键盘聚焦时留着；点画面切换播放；键盘 Space/K、←→、↑↓、M、F。</summary>
    const string Script = """
        const $=id=>document.getElementById(id),v=$('media'),frame=$('frame'),bar=$('bar'),seek=$('seek'),volume=$('volume');
        const STEP=5,IDLE=2500;let shown=true,timer,menu=null;
        const clock=s=>{if(!isFinite(s))return'0:00';const t=Math.max(0,Math.floor(s)),p=n=>String(n).padStart(2,'0'),h=Math.floor(t/3600),m=Math.floor(t%3600/60);return h>0?`${h}:${p(m)}:${p(t%60)}`:`${m}:${p(t%60)}`};
        const playing=()=>!v.paused&&!v.ended,duration=()=>isFinite(v.duration)?v.duration:0;
        const fill=r=>r.style.setProperty('--p',(r.max>r.min?(r.value-r.min)/(r.max-r.min)*100:0)+'%');
        function show(next){shown=next;bar.classList.toggle('hidden',!next);frame.classList.toggle('idle',!next&&playing())}
        const busy=()=>menu!==null||bar.matches(':hover');
        function wake(){show(true);clearTimeout(timer);if(playing())timer=setTimeout(()=>busy()?wake():show(false),IDLE)}
        function leave(){if(playing()&&!busy())show(false)}
        function time(){$('time').textContent=clock(v.currentTime)+' / '+clock(duration());seek.value=v.currentTime;seek.setAttribute('aria-valuetext',C.time.replace('\u0001',clock(v.currentTime)).replace('\u0002',clock(duration())));fill(seek)}
        function sync(){
          const p=$('play');p.innerHTML=playing()?C.icons.pause:C.icons.play;p.setAttribute('aria-label',playing()?C.pause:C.play);
          seek.max=duration()||1;seek.disabled=duration()===0;time();
          const m=$('mute'),quiet=v.muted||v.volume===0;m.innerHTML=quiet?C.icons.muted:C.icons.volume;m.setAttribute('aria-label',v.muted?C.unmute:C.mute);m.setAttribute('aria-pressed',v.muted);
          volume.value=v.muted?0:v.volume;fill(volume);
          check('speed',String(v.playbackRate));
          const showing=[...v.textTracks].findIndex(t=>t.mode==='showing');check('captions',showing<0?'':String(showing));
        }
        function check(kind,value){const list=document.querySelector(`[data-kind=${kind}]`);if(!list)return;for(const item of list.children)item.setAttribute('aria-checked',item.dataset.value===value)}
        const toggle=()=>v.paused?v.play().catch(()=>{}):v.pause();
        const seekTo=s=>{v.currentTime=Math.min(Math.max(s,0),duration());time()};
        const setVolume=x=>{v.volume=Math.min(Math.max(x,0),1);v.muted=v.volume===0};
        function setCaption(index){[...v.textTracks].forEach((t,i)=>t.mode=String(i)===index?'showing':'disabled');sync()}
        function open(kind){close();menu=document.querySelector(`[data-kind=${kind}]`);menu.hidden=false;$(kind).setAttribute('aria-expanded','true');(menu.querySelector('[aria-checked=true]')||menu.firstElementChild).focus();wake()}
        function close(refocus){if(!menu)return;const kind=menu.dataset.kind;menu.hidden=true;$(kind).setAttribute('aria-expanded','false');menu=null;if(refocus)$(kind).focus();wake()}
        for(const list of document.querySelectorAll('[role=menu]')){
          if(list.dataset.kind==='captions')[...v.textTracks].forEach((t,i)=>{const item=list.firstElementChild.cloneNode(true);item.dataset.value=String(i);item.lastChild.textContent=t.label;list.append(item)});
          list.addEventListener('click',e=>{const item=e.target.closest('[role=menuitemradio]');if(!item)return;if(list.dataset.kind==='speed')v.playbackRate=Number(item.dataset.value);else setCaption(item.dataset.value);close(true)});
          list.addEventListener('keydown',e=>{const items=[...list.children],at=items.indexOf(document.activeElement);
            if(e.key==='ArrowDown'||e.key==='ArrowUp'){e.preventDefault();items[(at+(e.key==='ArrowDown'?1:items.length-1))%items.length].focus()}
            else if(e.key==='Home'||e.key==='End'){e.preventDefault();items[e.key==='Home'?0:items.length-1].focus()}
            else if(e.key==='Enter'||e.key===' '){e.preventDefault();document.activeElement.click()}
            else if(e.key==='Escape'||e.key==='Tab'){if(e.key==='Escape')e.preventDefault();close(e.key==='Escape')}
            e.stopPropagation()});
        }
        for(const kind of ['captions','speed'])$(kind)?.addEventListener('click',()=>menu&&menu.dataset.kind===kind?close(true):open(kind));
        document.addEventListener('pointerdown',e=>{if(menu&&!e.target.closest('.menu'))close()});
        $('play').onclick=toggle;$('mute').onclick=()=>v.muted=!v.muted;
        seek.oninput=()=>seekTo(Number(seek.value));volume.oninput=()=>setVolume(Number(volume.value));
        const fs=$('fullscreen');if(!document.fullscreenEnabled)fs.remove();else fs.onclick=()=>document.fullscreenElement?document.exitFullscreen():frame.requestFullscreen();
        document.addEventListener('fullscreenchange',()=>{const on=!!document.fullscreenElement;fs.innerHTML=on?C.icons.minimize:C.icons.maximize;fs.setAttribute('aria-label',on?C.exitFullscreen:C.fullscreen)});
        bar.addEventListener('click',e=>e.stopPropagation());
        frame.addEventListener('click',toggle);frame.addEventListener('pointermove',wake);frame.addEventListener('pointerleave',leave);frame.addEventListener('focusin',wake);
        frame.addEventListener('keydown',e=>{
          if(e.altKey||e.ctrlKey||e.metaKey)return;
          const inControl=e.target.matches('input[type=range]'),onButton=e.target.closest('button');
          const act={k:toggle,' ':onButton?null:toggle,m:()=>v.muted=!v.muted,f:()=>fs.isConnected&&fs.click(),
            arrowleft:inControl?null:()=>seekTo(v.currentTime-STEP),arrowright:inControl?null:()=>seekTo(v.currentTime+STEP),
            arrowup:inControl?null:()=>setVolume(v.volume+.1),arrowdown:inControl?null:()=>setVolume(v.volume-.1)}[e.key.toLowerCase()];
          if(!act)return;e.preventDefault();act();wake()});
        for(const name of ['play','pause'])v.addEventListener(name,()=>{sync();wake()});
        for(const name of ['ended','durationchange','loadedmetadata','volumechange','ratechange'])v.addEventListener(name,sync);
        v.addEventListener('timeupdate',time);
        sync();
        """;
}
