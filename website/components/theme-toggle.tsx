"use client";

import { Moon, Sun } from "lucide-react";
import { useTheme } from "next-themes";

// Switches between light and dark. Both icons are rendered and CSS shows the one for the current
// theme, so the server-rendered button already matches before React takes over.
export function ThemeToggle() {
  const { resolvedTheme, setTheme } = useTheme();

  return (
    <button
      type="button"
      className="icon-link theme-toggle"
      aria-label="Switch between light and dark mode"
      title="Switch between light and dark mode"
      onClick={() => setTheme(resolvedTheme === "dark" ? "light" : "dark")}
    >
      <Sun className="theme-icon-light" size={17} strokeWidth={1.75} />
      <Moon className="theme-icon-dark" size={17} strokeWidth={1.75} />
    </button>
  );
}
