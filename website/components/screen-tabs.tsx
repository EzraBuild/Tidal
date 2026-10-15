"use client";

import Image, { type StaticImageData } from "next/image";
import { useRef, useState } from "react";
import popup from "@/public/screens/popup.png";
import display from "@/public/screens/display.png";
import adaptive from "@/public/screens/adaptive.png";
import shortcuts from "@/public/screens/shortcuts.png";

type Screen = { id: string; label: string; image: StaticImageData; alt: string; caption: string; compact?: boolean };

const screens: Screen[] = [
  {
    id: "tray",
    label: "tray",
    image: popup,
    compact: true,
    alt: "Tidal's tray popup: a brightness slider for each display, an input switcher, presets and Link all",
    caption: "Click the tray icon: a slider per display, the monitor's input, presets and Link all.",
  },
  {
    id: "display",
    label: "display",
    image: display,
    alt: "Tidal Settings for the laptop screen: lowest, current and highest brightness around a drawing of the laptop",
    caption: "One page per display: lowest, current and highest brightness, plus contrast and input where the monitor has them.",
  },
  {
    id: "adaptive",
    label: "adaptive",
    image: adaptive,
    alt: "Tidal's adaptive brightness page with four modes: manual, follow the sun, match the laptop and light sensor",
    caption: "Choose what brightness follows: you, the sun, the laptop screen or a light sensor.",
  },
  {
    id: "shortcuts",
    label: "shortcuts",
    image: shortcuts,
    alt: "Tidal's shortcuts page: brighter and dimmer keys, step size and which displays they move",
    caption: "Record your own keys, and choose whether they move every display or the one under the pointer.",
  },
];

export function ScreenTabs() {
  const [index, setIndex] = useState(0);
  const tabs = useRef<(HTMLButtonElement | null)[]>([]);
  const current = screens[index];

  function onKeyDown(event: React.KeyboardEvent) {
    const step = event.key === "ArrowRight" ? 1 : event.key === "ArrowLeft" ? -1 : 0;
    if (step === 0) return;
    event.preventDefault();
    const next = (index + step + screens.length) % screens.length;
    setIndex(next);
    tabs.current[next]?.focus();
  }

  return (
    <figure className="viewer">
      <div className="viewer-bar">
        <div className="viewer-tabs" role="tablist" aria-label="Tidal screens" onKeyDown={onKeyDown}>
          {screens.map((screen, i) => (
            <button
              key={screen.id}
              ref={(el) => {
                tabs.current[i] = el;
              }}
              role="tab"
              id={`tab-${screen.id}`}
              aria-selected={i === index}
              aria-controls="screen-panel"
              tabIndex={i === index ? 0 : -1}
              className="viewer-tab"
              onClick={() => setIndex(i)}
            >
              {screen.label}
            </button>
          ))}
        </div>
        <span className="viewer-count">
          {index + 1}/{screens.length}
        </span>
      </div>
      <div className="viewer-stage" role="tabpanel" id="screen-panel" aria-labelledby={`tab-${current.id}`}>
        {/* All four stay mounted (and loaded), so switching tabs never waits on the network. */}
        {screens.map((screen, i) => (
          <Image
            key={screen.id}
            src={screen.image}
            alt={screen.alt}
            hidden={i !== index}
            priority={i === 0}
            loading={i === 0 ? undefined : "eager"}
            sizes="(max-width: 900px) 100vw, 620px"
            className={screen.compact ? "shot shot-compact" : "shot"}
          />
        ))}
      </div>
      <figcaption className="viewer-caption">{current.caption}</figcaption>
    </figure>
  );
}
