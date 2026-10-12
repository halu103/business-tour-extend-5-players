import assert from 'node:assert/strict';
import test from 'node:test';
import { readFileSync } from 'node:fs';
import { languageTag, openGraphLocale } from '../src/lib/language-config.ts';
import { locales, localizeHref, extractLocaleFromUrl } from '../src/lib/paraglide/runtime.js';
import * as messages from '../src/lib/paraglide/messages.js';

test('all configured translation files are active', () => {
	const settings = JSON.parse(
		readFileSync(new URL('../project.inlang/settings.json', import.meta.url), 'utf8')
	);
	assert.deepEqual([...locales], settings.locales);
	assert.deepEqual([...locales], ['en', 'vi', 'zh', 'kr', 'jp', 'de']);
});
test('switching language preserves query and section hash', () => {
	for (const locale of locales) {
		const url = new URL(localizeHref('https://example.com/?source=guide#releases', { locale }));
		assert.equal(url.search, '?source=guide');
		assert.equal(url.hash, '#releases');
		assert.equal(extractLocaleFromUrl(url), locale);
	}
	const english = new URL(
		localizeHref('https://example.com/vi?source=guide#help', { locale: 'en' })
	);
	assert.equal(extractLocaleFromUrl(english), 'en');
	assert.equal(english.hash, '#help');
});
test('existing translations are honored; only absent keys use base English', () => {
	for (const locale of locales) {
		const source = JSON.parse(
			readFileSync(new URL('../messages/' + locale + '.json', import.meta.url), 'utf8')
		);
		const english = JSON.parse(
			readFileSync(new URL('../messages/en.json', import.meta.url), 'utf8')
		);
		for (const key of ['brand_name', 'language_label', 'feature_board_title']) {
			const expected = source[key] ?? english[key];
			assert.equal(messages[key]({}, { locale }), expected);
		}
	}
});
test('HTML/SEO language codes remain valid without renaming user message files', () => {
	assert.equal(languageTag('jp'), 'ja');
	assert.equal(languageTag('kr'), 'ko');
	assert.equal(openGraphLocale('jp'), 'ja_JP');
	assert.equal(openGraphLocale('kr'), 'ko_KR');
	assert.equal(openGraphLocale('vi'), 'vi_VN');
});
