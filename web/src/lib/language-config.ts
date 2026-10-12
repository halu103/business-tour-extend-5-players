// Keep existing message filenames/URL keys while using valid language tags
// in HTML and metadata; no user translation files need to be renamed.
export function languageTag(locale: string): string {
	return locale === 'jp' ? 'ja' : locale === 'kr' ? 'ko' : locale;
}

export function openGraphLocale(locale: string): string {
	return (
		(
			{ en: 'en_US', vi: 'vi_VN', zh: 'zh_CN', kr: 'ko_KR', jp: 'ja_JP', de: 'de_DE' } as Record<
				string,
				string
			>
		)[locale] ?? locale.replace('-', '_')
	);
}
