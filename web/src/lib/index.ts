import { env } from '$env/dynamic/public';
import { resolveRepositoryUrl } from './release-config';
export const REPOSITORY_URL = resolveRepositoryUrl(env.PUBLIC_REPOSITORY_URL);
