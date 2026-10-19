import type { NextRequest } from "next/server";

import {
  ProductionRoomFailure,
  deleteProductionRoomMessage,
  editProductionRoomMessage,
  setProductionRoomMessagePinned,
} from "@/lib/production-room-api";
import {
  assertSameOrigin,
  bearerToken,
  productionRoomError,
  productionRoomOk,
  readProductionRoomJson,
} from "../../shared";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

type Context = { params: Promise<{ id: string }> };

export async function PATCH(request: NextRequest, context: Context) {
  try {
    assertSameOrigin(request);
    const { id } = await context.params;
    const body = await readProductionRoomJson<{
      action?: string;
      body?: string;
      pinned?: boolean;
    }>(request);

    if (body.action === "edit") {
      return productionRoomOk(
        await editProductionRoomMessage(bearerToken(request), id, body.body || ""),
      );
    }
    if (body.action === "pin") {
      return productionRoomOk(
        await setProductionRoomMessagePinned(
          bearerToken(request),
          id,
          body.pinned as boolean,
        ),
      );
    }
    throw new ProductionRoomFailure("INVALID", "Unsupported production room message action.");
  } catch (error) {
    return productionRoomError(error);
  }
}

export async function DELETE(request: NextRequest, context: Context) {
  try {
    assertSameOrigin(request);
    const { id } = await context.params;
    return productionRoomOk(await deleteProductionRoomMessage(bearerToken(request), id));
  } catch (error) {
    return productionRoomError(error);
  }
}
