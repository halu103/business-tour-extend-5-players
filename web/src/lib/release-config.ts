export const DEFAULT_REPOSITORY_URL = 'https://github.com/halu103/business-tour-extend-5-players';

export function resolveRepositoryUrl(value?: string): string {
	try {
		const url = new URL(value || DEFAULT_REPOSITORY_URL);
		if (
			url.protocol !== 'https:' ||
			url.hostname !== 'github.com' ||
			url.username ||
			url.password ||
			!/^\/[A-Za-z0-9_-]+\/business-tour(?:-extend-5-players)?\/?$/.test(url.pathname)
		)
			return DEFAULT_REPOSITORY_URL;
		const path = url.pathname.replace(/\/$/, '');
		// GitHub redirects the old project slug; normalize to its actual URL
		// so legitimate release asset URLs pass the same-repository check.
		return path === '/halu103/business-tour' ? DEFAULT_REPOSITORY_URL : url.origin + path;
	} catch {
		return DEFAULT_REPOSITORY_URL;
	}
}

export function resolveSiteUrl(value?: string): string | null {
	if (!value) return null;
	try {
		const url = new URL(value);
		if (
			url.protocol !== 'https:' ||
			url.username ||
			url.password ||
			['localhost', '127.0.0.1', '0.0.0.0'].includes(url.hostname)
		)
			return null;
		url.search = '';
		url.hash = '';
		return url.href;
	} catch {
		return null;
	}
}
