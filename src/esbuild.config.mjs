import { build } from 'esbuild'
import { readFileSync, writeFileSync, copyFileSync, mkdirSync, rmSync } from 'fs'

const name = 'BabylonJS'
const outDir = '../dist'

rmSync(outDir, { recursive: true, force: true })
mkdirSync(`${outDir}/Scripts`, { recursive: true })

// The WebSharper-generated module imports the npm `@babylonjs/core` package
// (and the WebSharper runtime); bundle it all into a single IIFE.
await build({
  entryPoints: [`./build/${name}.js`],
  bundle: true,
  minify: true,
  format: 'iife',
  outfile: `${outDir}/Scripts/${name}.min.js`,
  globalName: 'wsbundle'
})

copyFileSync('./site.css', `${outDir}/site.css`)

// Fold the WebSharper head (linked resources) into the page.
const head = readFileSync(`./build/${name}.head.html`, 'utf8')
const html = readFileSync('./index.html', 'utf8').replace('<!-- ws-head -->', head)
writeFileSync(`${outDir}/index.html`, html)

console.log('Bundled:', name)