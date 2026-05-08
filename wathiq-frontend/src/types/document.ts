export type DocumentPriority = "Normal" | "Important" | "Urgent" | 0 | 1 | 2;
export type DocumentStatus =
  | "Draft"
  | "Processing"
  | "Submitted"
  | "UnderReview"
  | "Approved"
  | "Rejected"
  | "Published"
  | "Archived"
  | 0
  | 1
  | 2
  | 3
  | 4
  | 5
  | 6
  | 7;

export type Owner = {
  id: string;
  name: string;
  email?: string | null;
};

export type Metadata = {
  id?: string | null;
  description?: string | null;
  category?: string | null;
  tags?: string[] | null;
  department?: string | null;
  departmentId?: string | null;
  documentType?: string | null;
  expirationDate?: string | null;
  issuingEntity?: string | null;
  referenceNumber?: string | null;
  documentDate?: string | null;
  insights?: string[] | null;
  hasSignature?: boolean;
  signatures?: string[] | null;
  headers?: string[] | null;
  footers?: string[] | null;
  stamps?: string[] | null;
  rawExtractionJson?: string | null;
  createdAt?: string | null;
  updatedAt?: string | null;
};

export type Document = {
  id: string;
  documentId?: number | null;
  title?: string | null;
  content?: string | null;
  fileName?: string | null;
  contentType?: string | null;
  size?: number | null;
  fileHash?: string | null;
  createdAt?: string | null;
  updatedAt?: string | null;
  userId?: string | null;
  institutionId?: string | null;
  departmentId?: string | null;
  department?: string | null;
  priority?: DocumentPriority | null;
  isSensitive?: boolean | null;
  status?: DocumentStatus | null;
  submittedAt?: string | null;
  reviewStartedAt?: string | null;
  reviewedAt?: string | null;
  publishedAt?: string | null;
  archivedAt?: string | null;
  reviewedByUserId?: string | null;
  publishedByUserId?: string | null;
  archivedByUserId?: string | null;
  rejectionReason?: string | null;
  ownerName?: string | null;
  owner?: Owner | null;
  metadata?: Metadata | null;
};

export type DocumentTimelineEntry = {
  id?: string | null;
  action?: string | null;
  description?: string | null;
  createdAt?: string | null;
  userId?: string | null;
  userName?: string | null;
};

export type OcrProcessingResponse = {
  status: "processing";
  message?: string | null;
};

export type DocumentOcrText = {
  documentId: string;
  title?: string | null;
  status?: DocumentStatus | null;
  rawText: string;
  normalizedText: string;
  provider?: string | null;
  language?: string | null;
  pages?: number | null;
  extractedAt?: string | null;
};
