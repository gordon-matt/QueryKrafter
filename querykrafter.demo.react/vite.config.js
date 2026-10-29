import { cpSync, createReadStream, existsSync, statSync } from 'node:fs'
import path from 'node:path'
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import { output } from './scripts/paths.mjs'

const FOLDERS = ['_framework', '_content']

const CONTENT_TYPES = {
  '.js': 'text/javascript',
  '.mjs': 'text/javascript',
  '.wasm': 'application/wasm',
  '.json': 'application/json',
  '.css': 'text/css',
}

function queryKrafter() {
  return {
    name: 'querykrafter',
    configureServer(server) {
      server.middlewares.use((req, res, next) => {
        const url = decodeURIComponent((req.url ?? '').split('?')[0])
        if (!FOLDERS.some(folder => url.startsWith(`/${folder}/`))) {
          return next()
        }

        const file = path.join(output, url)
        if (!file.startsWith(output + path.sep) || !existsSync(file) || !statSync(file).isFile()) {
          return next()
        }

        res.setHeader('Content-Type', CONTENT_TYPES[path.extname(file)] ?? 'application/octet-stream')
        res.setHeader('Cache-Control', 'no-cache')
        createReadStream(file).pipe(res)
      })
    },
    writeBundle(options) {
      for (const folder of FOLDERS) {
        cpSync(path.join(output, folder), path.join(options.dir, folder), { recursive: true })
      }
    },
  }
}

export default defineConfig({
  plugins: [react(), queryKrafter()],
  server: { port: 5175, strictPort: true },
})
