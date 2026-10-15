import Image from "next/image";
import { LogoMark, Wordmark } from "@/components/logo";
import { ScreenTabs } from "@/components/screen-tabs";
import {
  ArrowIcon,
  DownloadIcon,
  GitHubIcon,
  LinkIcon,
  LockIcon,
  PlugIcon,
  SunIcon,
  SunsetIcon,
  TrayIcon,
  WarningIcon,
} from "@/components/icons";

const version = "0.1.0";
const repo = "https://github.com/EzraBuild/Tidal";
const installer = `${repo}/releases/latest/download/Tidal-win-Setup.exe`;
const portable = `${repo}/releases/latest/download/Tidal-win-Portable.zip`;

const features = [
  {
    tag: "DDC/CI",
    icon: SunIcon,
    title: "Real hardware brightness",
    text: "Talks to your monitors over DDC/CI, the same channel as their own buttons, so the backlight changes, not a dimmed picture. Laptop screens work too.",
  },
  {
    tag: "Adaptive",
    icon: SunsetIcon,
    title: "Follows your day",
    text: "Dim at night and brightest at midday, worked out offline from your location. Or let external screens match the laptop, or a light sensor.",
  },
  {
    tag: "Link all",
    icon: LinkIcon,
    title: "Every screen in step",
    text: "Move every display together, each within its own lowest and highest brightness, so a bright monitor and a dim laptop stay matched.",
  },
  {
    tag: "Inputs",
    icon: PlugIcon,
    title: "Inputs and volume",
    text: "Switch a monitor between DisplayPort, HDMI and USB-C, and set its speaker volume, when the monitor supports it.",
  },
  {
    tag: "Tray",
    icon: TrayIcon,
    title: "Lives in the tray",
    text: "One click away, light and dark mode, and a small level on screen when you use the keys. It tells you plainly when a display can't be controlled.",
  },
  {
    tag: "Private",
    icon: LockIcon,
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

const comingNext = [
  "Software dimming below 0%",
  "Dimming for monitors without DDC/CI",
  "Per-app brightness presets",
  "Start with Windows",
];

const faq = [
  {
    q: "Will it work with my monitor?",
    a: (
      <>
        Most external monitors from the last decade support DDC/CI, the standard Tidal uses. If one doesn&apos;t respond, turn on DDC/CI in
        the monitor&apos;s on-screen menu, or connect it directly instead of through a dock, KVM switch or adapter. Wireless and
        DisplayLink displays can&apos;t be controlled this way. Laptop screens use Windows&apos; own brightness control. Tidal shows which
        method each display uses, and why when one can&apos;t be controlled.
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
    a: <>No. There&apos;s no account, no analytics and no telemetry. Settings are saved in a file on your PC.</>,
  },
  {
    q: "How do I open it, and how do I uninstall it?",
    a: (
      <>
        Tidal lives in the notification area. On Windows 11, click the <strong>^</strong> arrow next to the clock and drag its icon
        onto the taskbar to keep it handy. To remove it: Settings › Apps › Installed apps › Tidal › Uninstall.
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
                  <DownloadIcon />
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
                    <Icon className="feature-icon" />
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
                <SunIcon />
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
                  <DownloadIcon />
                  Download installer
                </a>
                <a href={portable} className="btn btn-outline">
                  Portable .zip
                </a>
              </div>
              <div className="note" role="note">
                <WarningIcon className="note-icon" />
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
                      <span className="list-mark">✓</span>
                      {item}
                    </li>
                  ))}
                </ul>
              </div>
              <div className="list-block">
                <h3 className="list-title">Coming next</h3>
                <ul className="list">
                  {comingNext.map((item) => (
                    <li key={item}>
                      <span className="list-mark">[ ]</span>
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
                  <summary>{item.q}</summary>
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
              <ArrowIcon />
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
