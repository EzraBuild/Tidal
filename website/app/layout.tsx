import type { Metadata, Viewport } from "next";
import { Geist, Geist_Mono } from "next/font/google";
import "./globals.css";

const sans = Geist({ subsets: ["latin"], variable: "--font-sans" });
const mono = Geist_Mono({ subsets: ["latin"], variable: "--font-mono" });

const description =
  "Tidal is a free Windows tray app that changes the real brightness of your external monitors: sliders, keyboard shortcuts, and brightness that follows the sun.";

export const metadata: Metadata = {
  metadataBase: new URL("https://tidal-lemon.vercel.app"),
  title: "Tidal — brightness control for every monitor on Windows",
  description,
  openGraph: {
    title: "Tidal — every screen, just right",
    description: "Real hardware brightness for your external monitors, right from the Windows tray.",
    url: "/",
    images: ["/icon-512.png"],
  },
};

export const viewport: Viewport = {
  themeColor: [
    { media: "(prefers-color-scheme: dark)", color: "#000000" },
    { media: "(prefers-color-scheme: light)", color: "#ffffff" },
  ],
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en" className={`${sans.variable} ${mono.variable}`}>
      <body>{children}</body>
    </html>
  );
}
