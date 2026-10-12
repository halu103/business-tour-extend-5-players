import { env as privateEnv } from '$env/dynamic/private';
import { env as publicEnv } from '$env/dynamic/public';
import { resolveRepositoryUrl, resolveSiteUrl } from '#lib/release-config';
import { fetchReleaseData } from '#lib/server/releases';
import type { PageServerLoad } from './$types';

export const load: PageServerLoad = async ({ fetch, setHeaders }) => {
	const repository = resolveRepositoryUrl(publicEnv.PUBLIC_REPOSITORY_URL);
	const { releases, releasesLive, starCount } = await fetchReleaseData(
		fetch,
		repository,
		privateEnv.PRIVATE_GITHUB_TOKEN
	);
	setHeaders({
		'cache-control': releasesLive
			? 'public, max-age=300, s-maxage=3600, stale-while-revalidate=86400'
			: 'public, max-age=30, s-maxage=60'
	});
	const canonicalUrl = resolveSiteUrl(publicEnv.PUBLIC_SITE_URL);
	const imageUrl = resolveSiteUrl(publicEnv.PUBLIC_OG_IMAGE_URL);
	return { releases, releasesLive, starCount, seo: { canonicalUrl, imageUrl } };
};
