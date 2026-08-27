import $ from 'jquery';

// First-party scripts in wwwroot/js assume a browser global `$`.
window.$ = window.jQuery = $;
globalThis.$ = globalThis.jQuery = $;
