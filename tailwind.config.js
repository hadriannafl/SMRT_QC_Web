/** @type {import('tailwindcss').Config} */
module.exports = {
  /* Scan all Razor views and JS files for Tailwind class usage */
  content: [
    "./Views/**/*.cshtml",
    "./wwwroot/js/**/*.js",
  ],
  theme: {
    extend: {
      fontFamily: {
        /* Match the Inter font used throughout the app */
        inter: ['Inter', 'Segoe UI', 'sans-serif'],
      },
    },
  },
  plugins: [],
}
