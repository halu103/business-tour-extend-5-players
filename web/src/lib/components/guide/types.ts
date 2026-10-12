export type Release = {
	tag: string;
	version: string | null;
	name: string;
	publishedAt: string;
	pageUrl: string;
	prerelease: boolean;
	compatibility: string | null;
	zipUrl: string | null;
	zipSize: number | null;
	installerUrl: string | null;
	installerSize: number | null;
	checksumUrl: string | null;
	downloadCount: number | null;
};
