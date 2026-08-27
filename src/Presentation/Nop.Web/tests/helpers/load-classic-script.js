import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import vm from 'node:vm';
import $ from 'jquery';

const jsRoot = resolve(dirname(fileURLToPath(import.meta.url)), '../../wwwroot/js');

/**
 * Load a first-party wwwroot/js file as a classic (non-module) script.
 * Those files are not ESM and must stay unmodified until later tickets rewrite them.
 */
export function loadClassicNopScript(filename) {
  const filePath = resolve(jsRoot, filename);
  const sandbox = {
    window,
    document,
    $,
    jQuery: $,
    console,
  };
  sandbox.globalThis = sandbox;
  sandbox.self = window;
  vm.createContext(sandbox);
  vm.runInContext(readFileSync(filePath, 'utf8'), sandbox, { filename: filePath });
  return sandbox;
}
