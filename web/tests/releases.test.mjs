import assert from 'node:assert/strict';
import test from 'node:test';
import { readFileSync } from 'node:fs';
import { normalizeReleases, fetchReleaseData } from '../src/lib/server/releases.ts';
import { resolveRepositoryUrl, resolveSiteUrl } from '../src/lib/release-config.ts';

const repo = 'https://github.com/halu103/business-tour-extend-5-players';
function release(tag = 'v0.2.2', prerelease = false, version = tag.replace(/^v/, '')) {
	const name = `BusinessTourFiveRealms-${version}-win-x64.zip`;
	return {
		tag_name: tag,
		name: 'Five Realms',
		published_at: '2026-10-12T12:00:00Z',
		html_url: repo + '/releases/tag/' + tag,
		draft: false,
		prerelease,
		assets: [
			{
				name,
				browser_download_url: repo + '/releases/download/' + tag + '/' + name,
				size: 35_491_127,
				download_count: 8
			}
		]
	};
}

test('matches the versioned ZIP and accepts the real non-numeric Release tag', () => {
	for (const item of [release(), release('Release', false, '0.2.2')]) {
		const result = normalizeReleases([item], repo);
		assert.equal(result.length, 1);
		assert.equal(result[0].version, '0.2.2');
		assert.equal(result[0].compatibility, null);
		assert.equal(result[0].downloadCount, 8);
		assert.equal(result[0].checksumUrl, null);
		assert.equal(result[0].installerUrl, null);
	}
});
test('stable package precedes a newer preview; duplicates and drafts are omitted', () => {
	const preview = release('v0.3.0-preview.1', true);
	preview.published_at = '2026-10-13T00:00:00Z';
	assert.deepEqual(
		normalizeReleases(
			[preview, release(), release(), { ...release('v0.2.0'), draft: true }],
			repo
		).map((r) => r.tag),
		['v0.2.2', 'v0.3.0-preview.1']
	);
});
test('rejects unrelated, unsafe, duplicate and ambiguous assets without guessed links', () => {
	for (const mutate of [
		(r) => (r.assets[0].name = 'FFWFrostburn8-Windows-x64.zip'),
		(r) => (r.assets[0].browser_download_url = 'javascript:alert(1)'),
		(r) => (r.assets[0].browser_download_url = 'https://evil.example/mod.zip'),
		(r) =>
			(r.assets[0].browser_download_url = r.assets[0].browser_download_url.replace(
				'business-tour',
				'another-repo'
			)),
		(r) => (r.assets[0].size = -1),
		(r) => (r.assets[0].size = 1_000_000_000),
		(r) => r.assets.push({ ...r.assets[0] }),
		(r) => (r.published_at = 'invalid'),
		(r) => (r.html_url = 'https://evil.example/release')
	]) {
		const item = release();
		mutate(item);
		assert.equal(normalizeReleases([item], repo).length, 0);
	}
	const ambiguous = release('Release', false, '0.2.2');
	ambiguous.assets.push(release('Release', false, '0.2.1').assets[0]);
	assert.equal(normalizeReleases([ambiguous], repo).length, 0);
	assert.throws(() => normalizeReleases({ message: 'rate limited' }, repo));
});
test('checksum links are matched only when attached', () => {
	const item = release();
	const name = item.assets[0].name + '.sha256';
	item.assets.push({
		name,
		browser_download_url: repo + '/releases/download/v0.2.2/' + name,
		size: 66,
		download_count: 1
	});
	assert.equal(normalizeReleases([item], repo)[0].checksumUrl, item.assets[1].browser_download_url);
});
test('copied repository configuration falls back to Business Tour; canonical domain is never guessed', () => {
	assert.equal(resolveRepositoryUrl(undefined), repo);
	assert.equal(resolveRepositoryUrl('https://github.com/halu103/business-tour'), repo);
	assert.equal(
		resolveRepositoryUrl('https://github.com/halu103/farfarwest-frostburn-modkit'),
		repo
	);
	assert.equal(resolveRepositoryUrl('javascript:alert(1)'), repo);
	assert.equal(resolveSiteUrl(undefined), null);
	assert.equal(resolveSiteUrl('http://localhost:5178'), null);
	assert.equal(resolveSiteUrl('https://example.com/?tracking=1#hero'), 'https://example.com/');
});
test('API errors and malformed data return an honest empty state', async () => {
	for (const fetcher of [
		async () => {
			throw new Error('offline');
		},
		async () => new Response('{}', { status: 403 }),
		async () => Response.json({ bad: true })
	]) {
		const result = await fetchReleaseData(fetcher, repo);
		assert.deepEqual(result, { releases: [], releasesLive: false, starCount: null });
	}
});
test('only GitHub receives the server token; token is never serialized', async () => {
	const calls = [];
	const fetcher = async (url, init) => {
		calls.push({ url, init });
		return Response.json(
			url.includes('/releases?') ? [release('Release', false, '0.2.2')] : { stargazers_count: 1 }
		);
	};
	const result = await fetchReleaseData(fetcher, repo, 'test-only-token');
	assert.equal(result.releases.length, 1);
	assert.equal(result.starCount, 1);
	assert.equal(JSON.stringify(result).includes('test-only-token'), false);
	assert(
		calls.every(
			(c) =>
				new URL(c.url).hostname === 'api.github.com' &&
				c.init.headers.Authorization === 'Bearer test-only-token' &&
				c.init.signal instanceof AbortSignal
		)
	);
	await assert.rejects(fetchReleaseData(fetcher, 'https://evil.example/repo', 'test-only-token'));
});
test('English metadata and visible copy contain the right product and clear limits', () => {
	const messages = JSON.parse(
		readFileSync(new URL('../messages/en.json', import.meta.url), 'utf8')
	);
	assert(messages.meta_title.includes('Business Tour'));
	assert(messages.meta_description.includes('40-cell pentagonal'));
	assert(messages.meta_description.length >= 140 && messages.meta_description.length <= 180);
	assert(!/Far Far West|FFWFrostburn|UE4SS|Allow mods/.test(JSON.stringify(messages)));
	assert(messages.critical_body.includes('Every participant'));
	assert(messages.feature_connection_body.includes('do not guarantee'));
	const settings = JSON.parse(
		readFileSync(new URL('../project.inlang/settings.json', import.meta.url), 'utf8')
	);
	assert.deepEqual(settings.locales, ['en', 'vi', 'zh', 'kr', 'jp', 'de']);
});
