import type { Config } from 'tailwindcss'

// Colors reference CSS custom properties (see src/assets/styles/tokens.css) so a single source
// drives both Tailwind utilities and component CSS, and the theme toggle just flips `data-theme`.
export default {
  content: ['./index.html', './src/**/*.{vue,ts}'],
  theme: {
    extend: {
      colors: {
        bg: 'var(--bg)',
        surface: 'var(--surface)',
        'surface-2': 'var(--surface-2)',
        sidebar: {
          DEFAULT: 'var(--sidebar)',
          text: 'var(--sidebar-text)',
          muted: 'var(--sidebar-muted)',
        },
        border: 'var(--border)',
        text: {
          DEFAULT: 'var(--text)',
          muted: 'var(--text-muted)',
        },
        accent: {
          DEFAULT: 'var(--accent)',
          hover: 'var(--accent-hover)',
          active: 'var(--accent-active)',
          soft: 'var(--accent-soft)',
          contrast: 'var(--accent-contrast)',
        },
        positive: 'var(--positive)',
        negative: 'var(--negative)',
        warning: 'var(--warning)',
        info: 'var(--info)',
      },
      fontFamily: {
        sans: ['Plus Jakarta Sans', 'ui-sans-serif', 'system-ui', 'sans-serif'],
      },
      borderRadius: {
        card: '16px',
        control: '10px',
      },
      boxShadow: {
        card: 'var(--shadow)',
      },
      fontSize: {
        kpi: ['1.375rem', { lineHeight: '1.2', fontWeight: '600' }],
      },
    },
  },
  plugins: [],
} satisfies Config
