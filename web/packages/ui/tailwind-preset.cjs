/**
 * Shared Tailwind preset — KFS brand tokens (per spec section 15.7).
 * Each app extends this in its own tailwind.config.ts and adds its own `content` glob.
 */
module.exports = {
  theme: {
    extend: {
      colors: {
        kfs: {
          forest: {
            DEFAULT: '#0d3128',
            50:  '#f0f5f3',
            100: '#dce9e5',
            500: '#1d4f43',
            600: '#163e34',
            700: '#0d3128',
            800: '#0a2620',
            900: '#061814'
          },
          sage: {
            DEFAULT: '#548b7d',
            50:  '#f3f7f6',
            100: '#e0eae7',
            500: '#548b7d',
            600: '#456f64',
            700: '#365650'
          },
          gold: {
            DEFAULT: '#a08b16',
            50:  '#fbf8e9',
            100: '#f5edc4',
            500: '#a08b16',
            600: '#806f12',
            700: '#60530d'
          }
        }
      },
      fontFamily: {
        sans:   ['"Source Sans 3 Variable"', 'system-ui', 'sans-serif'],
        arabic: ['"IBM Plex Sans Arabic"', 'Tahoma', 'sans-serif']
      },
      borderRadius: {
        DEFAULT: '0.375rem'
      },
      boxShadow: {
        'kfs-card': '0 1px 2px rgba(13, 49, 40, 0.06), 0 1px 3px rgba(13, 49, 40, 0.04)'
      }
    }
  },
  plugins: []
};
