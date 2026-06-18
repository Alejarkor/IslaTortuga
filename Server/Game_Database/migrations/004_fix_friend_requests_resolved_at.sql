-- Fix: friend_requests.resolved_at era NOT NULL sin DEFAULT, lo que hacía fallar
-- la creación de una solicitud nueva (pending), que no tiene fecha de resolución
-- hasta que se acepta/rechaza/cancela. La hacemos anulable.
ALTER TABLE friend_requests ALTER COLUMN resolved_at DROP NOT NULL;
