import type { Metadata } from "next";
import { notFound } from "next/navigation";

import { PublicServiceCategoryPage } from "@/components/PublicServicesPages";
import { buildPublicMetadata } from "@/lib/public-metadata";
import { SERVICE_CATEGORY_BY_SLUG } from "@/lib/public-services";

type CategoryPageProps = {
  params: Promise<{ category: string }>;
};

export const dynamic = "force-dynamic";

export async function generateMetadata({ params }: CategoryPageProps): Promise<Metadata> {
  const { category: slug } = await params;
  const category = SERVICE_CATEGORY_BY_SLUG[
    slug as keyof typeof SERVICE_CATEGORY_BY_SLUG
  ];

  if (!category) {
    return {};
  }

  return buildPublicMetadata({
    title: category.title,
    description: category.description,
    path: `/services/${category.slug}`,
  });
}

export default async function ServiceCategoryRoute({ params }: CategoryPageProps) {
  const { category: slug } = await params;
  const category = SERVICE_CATEGORY_BY_SLUG[
    slug as keyof typeof SERVICE_CATEGORY_BY_SLUG
  ];

  if (!category) {
    notFound();
  }

  return <PublicServiceCategoryPage category={category} />;
}
