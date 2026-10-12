import type { Release } from '../components/guide/types';

type RecordValue = Record<string, unknown>;
const record = (value: unknown): value is RecordValue =>
	value !== null && typeof value === 'object' && !Array.isArray(value);
const validNumber = (value: unknown): value is number =>
	typeof value === 'number' && Number.isSafeInteger(value) && value >= 0;

function githubUrl(value: unknown, repository: string, path: string): string | null {
	if (typeof value !== 'string') return null;
	try {
		const url = new URL(value),
			repo = new URL(repository);
		if (
			url.protocol !== 'https:' ||
			url.hostname !== 'github.com' ||
			url.username ||
			url.password ||
			url.search ||
			url.hash ||
			decodeURIComponent(url.pathname) !== repo.pathname + path
		)
			return null;
		return url.href;
	} catch {
		return null;
	}
}

export function normalizeReleases(value: unknown, repository: string): Release[] {
	if (!Array.isArray(value)) throw new Error('Invalid release response');
	const result: Release[] = [];
	const seen = new Set<string>();
	for (const item of value) {
		if (
			!record(item) ||
			item.draft !== false ||
			typeof item.prerelease !== 'boolean' ||
			typeof item.tag_name !== 'string' ||
			typeof item.published_at !== 'string' ||
			!Number.isFinite(Date.parse(item.published_at)) ||
			!Array.isArray(item.assets)
		)
			continue;
		const tag = item.tag_name;
		if (!tag || tag.length > 128 || /[\u0000-\u001f]/.test(tag) || seen.has(tag)) continue;
		const tagVersion = tag.replace(/^v/, '');
		const pageUrl = githubUrl(item.html_url, repository, '/releases/tag/' + tag);
		if (!pageUrl) continue;
		const assets = item.assets.filter((asset): asset is RecordValue => record(asset));
		const bundlePattern =
			/^BusinessTourFiveRealms-(\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?)-win-x64\.zip$/;
		const bundles = assets.filter(
			(entry) => typeof entry.name === 'string' && bundlePattern.test(entry.name)
		);
		// Some published tags are named "Release" rather than v0.2.2. Derive
		// the package version from the actual attached ZIP, never from a guess.
		const matchingBundle = bundles.find(
			(entry) => (entry.name as string).match(bundlePattern)?.[1] === tagVersion
		);
		const bundle = matchingBundle ?? (bundles.length === 1 ? bundles[0] : null);
		const zipName = bundle ? (bundle.name as string) : null;
		function asset(name: string) {
			const entries = assets.filter((entry) => entry.name === name);
			if (entries.length !== 1) return null;
			const entry = entries[0];
			const url = githubUrl(
				entry.browser_download_url,
				repository,
				'/releases/download/' + tag + '/' + name
			);
			if (!url || !validNumber(entry.size) || entry.size <= 0 || entry.size > 100_000_000)
				return null;
			return {
				url,
				size: entry.size,
				count: validNumber(entry.download_count) ? entry.download_count : null
			};
		}
		const zip = zipName ? asset(zipName) : null;
		const installer = asset('BusinessTourFiveRealms-Setup.exe');
		if (!zip && !installer) continue;
		const checksum =
			(zipName ? asset(zipName + '.sha256') || asset(zipName + '.sha256.txt') : null) ||
			asset('SHA256SUMS.txt');
		const counts = [zip, installer].filter((entry) => entry !== null).map((entry) => entry.count);
		result.push({
			tag,
			version: zip && zipName ? (zipName.match(bundlePattern)?.[1] ?? null) : null,
			name: typeof item.name === 'string' && item.name.trim() ? item.name : tag,
			publishedAt: item.published_at,
			pageUrl,
			prerelease: item.prerelease,
			compatibility: null,
			zipUrl: zip?.url ?? null,
			zipSize: zip?.size ?? null,
			installerUrl: installer?.url ?? null,
			installerSize: installer?.size ?? null,
			checksumUrl: checksum?.url ?? null,
			downloadCount: counts.every((count) => count !== null)
				? counts.reduce<number>((sum, count) => sum + (count ?? 0), 0)
				: null
		});
		seen.add(tag);
	}
	return result.sort(
		(a, b) =>
			Number(a.prerelease) - Number(b.prerelease) ||
			Date.parse(b.publishedAt) - Date.parse(a.publishedAt)
	);
}

export async function fetchReleaseData(fetcher: typeof fetch, repository: string, token?: string) {
	const repo = new URL(repository);
	if (
		repo.protocol !== 'https:' ||
		repo.hostname !== 'github.com' ||
		repo.username ||
		repo.password ||
		!/^\/[A-Za-z0-9_-]+\/business-tour(?:-extend-5-players)?$/.test(repo.pathname)
	)
		throw new Error('Invalid source repository');
	const api = 'https://api.github.com/repos' + repo.pathname;
	const headers: Record<string, string> = {
		Accept: 'application/vnd.github+json',
		'User-Agent': 'BusinessTourFiveRealms-web',
		'X-GitHub-Api-Version': '2022-11-28'
	};
	if (token) headers.Authorization = 'Bearer ' + token;
	async function getJson(url: string): Promise<unknown> {
		const response = await fetcher(url, { headers, signal: AbortSignal.timeout(6000) });
		if (!response.ok) throw new Error('Release service unavailable');
		return response.json();
	}
	const [releaseResult, repositoryResult] = await Promise.allSettled([
		getJson(api + '/releases?per_page=20'),
		getJson(api)
	]);
	let releases: Release[] = [],
		releasesLive = false,
		starCount: number | null = null;
	if (releaseResult.status === 'fulfilled') {
		try {
			releases = normalizeReleases(releaseResult.value, repository);
			releasesLive = true;
		} catch {
			/* No invented download fallback. */
		}
	}
	if (repositoryResult.status === 'fulfilled' && record(repositoryResult.value)) {
		const count = repositoryResult.value.stargazers_count;
		if (validNumber(count)) starCount = count;
	}
	return { releases, releasesLive, starCount };
}
