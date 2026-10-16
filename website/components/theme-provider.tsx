"use client";

import { ThemeProvider as NextThemesProvider } from "next-themes";

// Light and dark mode: follows the system until the visitor picks one, and remembers the choice.
// next-themes sets data-theme on <html> before the page paints, so it never flashes the wrong theme.
export function ThemeProvider({ children }: { children: React.ReactNode }) {
  return (
    <NextThemesProvider attribute="data-theme" defaultTheme="system" enableSystem disableTransitionOnChange>
      {children}
    </NextThemesProvider>
  );
}
