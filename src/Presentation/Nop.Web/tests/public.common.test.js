import { beforeEach, describe, expect, it } from 'vitest';
import { loadClassicNopScript } from './helpers/load-classic-script.js';

const { htmlEncode, htmlDecode, addAntiForgeryToken } = loadClassicNopScript('public.common.js');

describe('htmlEncode / htmlDecode', () => {
  it('encodes HTML special characters', () => {
    expect(htmlEncode('<script>alert(1)</script>')).toBe('&lt;script&gt;alert(1)&lt;/script&gt;');
    expect(htmlEncode('a & b')).toBe('a &amp; b');
  });

  it('decodes HTML entities back to text', () => {
    expect(htmlDecode('&lt;b&gt;hi&lt;/b&gt;')).toBe('<b>hi</b>');
    expect(htmlDecode('a &amp; b')).toBe('a & b');
  });

  it('round-trips a string through encode then decode', () => {
    const original = '<img src=x onerror=alert(1)> & "quotes"';
    expect(htmlDecode(htmlEncode(original))).toBe(original);
  });
});

describe('addAntiForgeryToken', () => {
  beforeEach(() => {
    document.body.innerHTML = '';
  });

  it('reads __RequestVerificationToken from the DOM fixture', () => {
    document.body.innerHTML = '<input name="__RequestVerificationToken" type="hidden" value="csrf-token-abc" />';

    const data = addAntiForgeryToken({ productId: 42 });

    expect(data).toEqual({
      productId: 42,
      __RequestVerificationToken: 'csrf-token-abc',
    });
  });

  it('creates a new object when data is missing and still copies the token', () => {
    document.body.innerHTML = '<input name="__RequestVerificationToken" value="tok" />';

    expect(addAntiForgeryToken()).toEqual({ __RequestVerificationToken: 'tok' });
    expect(addAntiForgeryToken(null)).toEqual({ __RequestVerificationToken: 'tok' });
  });

  it('mutates the original data object in place', () => {
    document.body.innerHTML = '<input name="__RequestVerificationToken" value="tok" />';
    const data = { a: 1 };

    expect(addAntiForgeryToken(data)).toBe(data);
    expect(data.__RequestVerificationToken).toBe('tok');
  });

  it('leaves data unchanged when the token input is absent', () => {
    const data = { a: 1 };

    expect(addAntiForgeryToken(data)).toEqual({ a: 1 });
    expect(data).not.toHaveProperty('__RequestVerificationToken');
  });

  it('uses the first token when multiple matching inputs exist', () => {
    document.body.innerHTML = `
      <input name="__RequestVerificationToken" value="first" />
      <input name="__RequestVerificationToken" value="second" />
    `;

    expect(addAntiForgeryToken({}).__RequestVerificationToken).toBe('first');
  });
});
