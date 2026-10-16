import Image from "next/image";
import { LogoMark, Wordmark } from "@/components/logo";
import { ScreenTabs } from "@/components/screen-tabs";
import { ThemeToggle } from "@/components/theme-toggle";
import { GitHubIcon } from "@/components/icons";
import {
  ArrowRight,
  Cable,
  Check,
  ChevronDown,
  Download,
  LayoutGrid,
  Link,
  Moon,
  PanelBottom,
  ShieldCheck,
  SlidersHorizontal,
  Sun,
  Sunset,
  TriangleAlert,
  type LucideIcon,
} from "lucide-react";

const version = "0.1.5";
const repo = "https://github.com/EzraBuild/Tidal";
const installer = `${repo}/releases/latest/download/Tidal-win-Setup.exe`;
const portable = `${repo}/releases/latest/download/Tidal-win-Portable.zip`;

const features: { tag: string; icon: LucideIcon; title: string; text: string }[] = [
  {
    tag: "DDC/CI",
    icon: Sun,
    title: "Real hardware brightness",
    text: "Talks to your monitors over DDC/CI, the same channel as their own buttons, so the backlight changes, not a dimmed picture. Laptop screens work too.",
  },
  {
    tag: "Adaptive",
    icon: Sunset,
    title: "Follows your day",
    text: "Dim at night and brightest at midday, worked out offline from your location. Or let external screens match the laptop, or a light sensor.",
  },
  {
    tag: "Dimming",
    icon: Moon,
    title: "Darker than 0%",
    text: "Turn on Dim below 0 and keep going past a monitor's lowest brightness, in software. Monitors without DDC/CI get a slider that dims them the same way.",
  },
  {
    tag: "Apps",
    icon: LayoutGrid,
    title: "A brightness for each app",
    text: "Give your video player, a game or anything else its own brightness. The screen showing it switches while it's in front, and goes back when you switch away.",
  },
  {
    tag: "Link all",
    icon: Link,
    title: "Every screen in step",
    text: "Move every display together, each within its own range, so a bright monitor and a dim laptop stay matched.",
  },
  {
    tag: "Limits",
    icon: SlidersHorizontal,
    title: "Your range, per display",
    text: "Set a lowest and highest brightness for each display. Sliders, shortcuts, presets and adaptive brightness all stay inside it.",
  },
  {
    tag: "Inputs",
    icon: Cable,
    title: "Inputs and volume",
    text: "Switch a monitor between DisplayPort, HDMI and USB-C, and set its speaker volume, when the monitor supports it.",
  },
  {
    tag: "Tray",
    icon: PanelBottom,
    title: "Lives in the tray",
    text: "One click away, in light and dark mode, with a small level on screen when you use the keys. It tells you plainly when a display can't be controlled.",
  },
  {
    tag: "Private",
    icon: ShieldCheck,
    title: "Nothing leaves your PC",
    text: "No account, no analytics, no telemetry. Sunrise and sunset are calculated on your PC, and settings are a file you own.",
  },
];

const facts = [
  { label: "Runs on", value: "Windows 10 & 11" },
  { label: "Controls", value: "DDC/CI + WMI" },
  { label: "Installs", value: "No admin rights" },
  { label: "Collects", value: "Nothing" },
];

const requirements = [
  "Windows 10 (version 2004 or later) or Windows 11, 64-bit",
  "Installs just for you: no admin rights, no account",
  "External monitors need DDC/CI, which most have (usually on in the monitor's menu)",
];

const faq = [
  {
    q: "Will it work with my monitor?",
    a: (
      <>
        Most external monitors from the last decade support DDC/CI, the standard Tidal uses. If one doesn&apos;t respond, turn on DDC/CI in
        the monitor&apos;s on-screen menu, or connect it directly instead of through a dock, KVM switch or adapter. Wireless and
        DisplayLink displays can&apos;t be controlled this way, so Tidal dims them in software instead. Laptop screens use Windows&apos;
        own brightness control. Tidal shows which method each display uses, and why when one can&apos;t be controlled.
      </>
    ),
  },
  {
    q: "Why does Windows warn me when I open it?",
    a: (
      <>
        Windows SmartScreen warns about apps that aren&apos;t code-signed and aren&apos;t yet widely downloaded. This preview isn&apos;t signed
        yet. Click <strong>More info</strong>, then <strong>Run anyway</strong>.
      </>
    ),
  },
  {
    q: "Does Tidal use my location?",
    a: (
      <>
        Only if you choose <em>Follow the sun</em> and press the location button; you can also type coordinates. The location is
        rounded to about a kilometre, stays on your PC, and sunrise and sunset are calculated offline.
      </>
    ),
  },
  {
    q: "Does it collect any data?",
    a: (
      <>
        No. There&apos;s no account, no analytics and no telemetry. Settings are saved in a file on your PC. The only thing Tidal
        asks the internet is whether GitHub has a newer version, which you can turn off in Settings › General.
      </>
    ),
  },
  {
    q: "How do I get new versions?",
    a: (
      <>
        From version 0.1.5, Tidal updates itself: it checks for a new version every few hours, downloads it in the background
        and installs it when you click <strong>Restart</strong> in its popup, or the next time it quits. Older versions: download
        the installer again and run it.
      </>
    ),
  },
  {
    q: "How do I open it, and how do I uninstall it?",
    a: (
      <>
        Tidal starts with Windows and lives in the notification area. On Windows 11, click the <strong>^</strong> arrow next to the
        clock and drag its icon onto the taskbar to keep it handy. You can turn off starting with Windows in Tidal&apos;s Settings ›
        General. To remove Tidal: Settings › Apps › Installed apps › Tidal › Uninstall.
      </>
    ),
  },
];

function Eyebrow({ children }: { children: React.ReactNode }) {
  return <p className="eyebrow">[ {children} ]</p>;
}

function Keys({ keys }: { keys: string[] }) {
  return (
    <span className="keys">
      {keys.map((key, i) => (
        <span key={key} className="keys-item">
          {i > 0 && <span className="keys-plus">+</span>}
          <kbd>{key}</kbd>
        </span>
      ))}
    </span>
  );
}

export default function Home() {
  return (
    <>
      <header className="nav">
        <div className="frame nav-inner">
          <a href="/" className="nav-brand" aria-label="Tidal home">
            <Wordmark />
          </a>
          <nav className="nav-links" aria-label="Sections">
            <a href="#features">Features</a>
            <a href="#shortcuts">Shortcuts</a>
            <a href="#faq">FAQ</a>
          </nav>
          <div className="nav-actions">
            <a href={repo} className="icon-link" aria-label="Tidal on GitHub">
              <GitHubIcon />
            </a>
            <ThemeToggle />
            <a href="#download" className="btn btn-primary btn-small">
              Download
            </a>
          </div>
        </div>
      </header>

      <main>
        <section className="band hero">
          <div className="frame hero-inner">
            <div className="hero-copy">
              <p className="pill">
                <span className="pill-dot" />
                v{version} preview · free for Windows
              </p>
              <h1>
                Brightness control for <span className="muted">every monitor</span> on Windows.
              </h1>
              <p className="lede">
                Tidal changes the <em>real</em> brightness of your external monitors, right from the tray. Sliders, keyboard shortcuts,
                and brightness that follows the sun, without digging through monitor buttons.
              </p>
              <div className="actions">
                <a href={installer} className="btn btn-primary">
                  <Download size={18} />
                  Download for Windows
                </a>
                <a href={repo} className="btn btn-outline">
                  <GitHubIcon />
                  View source
                </a>
              </div>
              <p className="fineprint">64-bit · Installs per user · No telemetry</p>
            </div>
            <ScreenTabs />
          </div>
        </section>

        <section className="band" aria-label="At a glance">
          <div className="frame facts">
            {facts.map((fact) => (
              <div key={fact.label} className="fact">
                <span className="fact-label">{fact.label}</span>
                <span className="fact-value">{fact.value}</span>
              </div>
            ))}
          </div>
        </section>

        <section className="band" id="features">
          <div className="frame">
            <div className="section-head">
              <Eyebrow>Features</Eyebrow>
              <h2>Everything your monitor&apos;s buttons should have been.</h2>
              <p className="section-lede">One place for every screen you plug in, with the controls the on-screen menu buries.</p>
            </div>
            <div className="feature-grid">
              {features.map(({ tag, icon: Icon, title, text }) => (
                <article key={tag} className="feature">
                  <div className="feature-top">
                    <Icon className="feature-icon" size={20} strokeWidth={1.5} />
                    <span className="feature-tag">{tag}</span>
                  </div>
                  <h3>{title}</h3>
                  <p>{text}</p>
                </article>
              ))}
            </div>
          </div>
        </section>

        <section className="band" id="shortcuts">
          <div className="frame split">
            <div className="split-copy">
              <Eyebrow>Shortcuts</Eyebrow>
              <h2>Brightness from any app.</h2>
              <p className="section-lede">
                Press the keys in any app and hold them to keep going. A small level appears on each display as it changes. Record your
                own keys in Settings.
              </p>
            </div>
            <div className="key-panel">
              <div className="key-row">
                <span className="key-label">Brighter</span>
                <Keys keys={["Ctrl", "Alt", "PageUp"]} />
              </div>
              <div className="key-row">
                <span className="key-label">Dimmer</span>
                <Keys keys={["Ctrl", "Alt", "PageDown"]} />
              </div>
              <div className="osd" aria-hidden="true">
                <Sun size={18} strokeWidth={1.75} />
                <span className="osd-track">
                  <span className="osd-fill" />
                </span>
                <span className="osd-value">65</span>
              </div>
            </div>
          </div>
        </section>

        <section className="band" id="download">
          <div className="frame split">
            <div className="split-copy download-copy">
              <Image src="/icon-512.png" alt="" width={64} height={64} className="app-icon" />
              <Eyebrow>Download</Eyebrow>
              <h2>Get Tidal.</h2>
              <p className="fineprint">Version {version} · preview</p>
              <div className="actions">
                <a href={installer} className="btn btn-primary">
                  <Download size={18} />
                  Download installer
                </a>
                <a href={portable} className="btn btn-outline">
                  Portable .zip
                </a>
              </div>
              <div className="note" role="note">
                <TriangleAlert className="note-icon" size={18} strokeWidth={1.75} />
                <p>
                  Windows may say <q>Windows protected your PC</q>: this preview isn&apos;t code-signed yet. Click{" "}
                  <strong>More info</strong>, then <strong>Run anyway</strong>.
                </p>
              </div>
            </div>
            <div className="lists">
              <div className="list-block">
                <h3 className="list-title">Requirements</h3>
                <ul className="list">
                  {requirements.map((item) => (
                    <li key={item}>
                      <Check className="list-mark" size={16} strokeWidth={2} />
                      {item}
                    </li>
                  ))}
                </ul>
              </div>
            </div>
          </div>
        </section>

        <section className="band" id="faq">
          <div className="frame split">
            <div className="split-copy">
              <Eyebrow>FAQ</Eyebrow>
              <h2>Questions.</h2>
              <p className="section-lede">
                Something else? <a href={`${repo}/issues`}>Open an issue on GitHub</a>.
              </p>
            </div>
            <div className="faq">
              {faq.map((item) => (
                <details key={item.q}>
                  <summary>
                    {item.q}
                    <ChevronDown className="faq-chevron" size={16} />
                  </summary>
                  <p>{item.a}</p>
                </details>
              ))}
            </div>
          </div>
        </section>

        <section className="band cta-band">
          <div className="frame cta">
            <LogoMark size={36} />
            <h2>Every screen, just right.</h2>
            <a href={installer} className="btn btn-primary">
              Download for Windows
              <ArrowRight size={18} />
            </a>
          </div>
        </section>
      </main>

      <footer className="band footer">
        <div className="frame footer-inner">
          <span>© 2026 Tidal</span>
          <nav aria-label="Footer">
            <a href={repo}>Source</a>
            <a href={`${repo}/releases`}>Releases</a>
            <a href={`${repo}/issues`}>Report a problem</a>
          </nav>
        </div>
      </footer>
    </>
  );
}
