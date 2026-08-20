import Image from "next/image";

const logoSources = {
  default: "/brand/logo/zachary-it-logo.svg",
  dark: "/brand/logo/zachary-it-logo-dark.svg",
  light: "/brand/logo/zachary-it-logo-light.svg",
  symbol: "/brand/logo/zachary-it-symbol.svg",
} as const;

type BrandLogoVariant = keyof typeof logoSources;

type BrandLogoProps = {
  className?: string;
  priority?: boolean;
  variant?: BrandLogoVariant;
};

/** Official Zachary IT artwork only; never rebuild the monogram in code. */
export function BrandLogo({
  className,
  priority = false,
  variant = "default",
}: BrandLogoProps) {
  const isSymbol = variant === "symbol";

  return (
    <Image
      alt="Zachary IT"
      className={className}
      height={isSymbol ? 48 : 64}
      priority={priority}
      src={logoSources[variant]}
      style={{ height: "auto" }}
      unoptimized
      width={isSymbol ? 48 : 340}
    />
  );
}
