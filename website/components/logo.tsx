// Tidal's ring-and-tide mark, same geometry as src/Tidal.App/Tray/Logo.cs (a 24x24 grid).
export function LogoMark({ size = 20 }: { size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" aria-hidden="true" className="logo-mark">
      <defs>
        <clipPath id="tidal-water">
          <circle cx="12" cy="12" r="6.75" />
        </clipPath>
      </defs>
      <circle cx="12" cy="12" r="9.25" fill="none" stroke="currentColor" strokeWidth="1.6" />
      <path d="M 4,12.5 Q 8,8.5 12,12.5 T 20,12.5 V 21 H 4 Z" fill="currentColor" clipPath="url(#tidal-water)" />
    </svg>
  );
}

export function Wordmark() {
  return (
    <span className="wordmark">
      <LogoMark />
      <span>TIDAL.</span>
    </span>
  );
}
