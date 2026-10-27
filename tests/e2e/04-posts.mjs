// Posts: network feed, likes, comments, ownership, soft delete and post notifications.
import { call, connect, customer, suite, waitForNotification } from "./lib.mjs";

export default async function run() {
  const t = suite("Posts");
  const [author, reader, stranger] = await Promise.all([customer("post-author"), customer("post-reader"), customer("post-stranger")]);
  const feed = async (who, scope) => (await call("GET", `/posts/feed?scope=${scope}&pageSize=50`, who.token)).json?.data ?? [];

  const post = (await call("POST", "/posts", author.token, { content: "E2E: finished a 40 ft boundary wall today." })).json?.data?.id;
  t.check("author posts", !!post);

  t.section("Feed");
  t.check("not in reader's network feed before connecting", !(await feed(reader, "network")).some(p => p.id === post));
  t.check("in reader's 'everyone' feed", (await feed(reader, "all")).some(p => p.id === post));
  await connect(reader, author);
  const seen = (await feed(reader, "network")).find(p => p.id === post);
  t.check("in reader's network feed after connecting, with author name", seen?.authorName === author.name && seen.isMine === false, seen);

  t.section("Likes");
  const like = await call("POST", `/posts/${post}/like`, reader.token);
  t.check("like is idempotent", like.json?.data?.likeCount === 1 && (await call("POST", `/posts/${post}/like`, reader.token)).json?.data?.likeCount === 1);
  t.check("author notified once", (await waitForNotification(author, n => n.type === "PostLiked" && n.referenceId === post)).length === 1);
  await call("DELETE", `/posts/${post}/like`, reader.token);
  await call("POST", `/posts/${post}/like`, reader.token);
  await new Promise(resolve => setTimeout(resolve, 3000));
  t.check("unlike + like again does not notify twice", (await waitForNotification(author, n => n.type === "PostLiked" && n.referenceId === post)).length === 1);

  t.section("Comments");
  const comment = (await call("POST", "/comments", reader.token, { postId: post, content: "Neat brickwork!" })).json?.data;
  t.check("reader comments and may edit/delete it", comment?.canEdit === true && comment?.canDelete === true);
  t.check("author notified of the comment", (await waitForNotification(author, n => n.type === "PostCommented" && n.referenceId === post)).length === 1);
  t.check("post author cannot edit the comment (403)", (await call("PUT", `/comments/${comment.commentId}`, author.token, { content: "x" })).status === 403);
  t.check("stranger cannot delete it (403)", (await call("DELETE", `/comments/${comment.commentId}`, stranger.token)).status === 403);
  t.check("comment author edits (isEdited)", (await call("PUT", `/comments/${comment.commentId}`, reader.token, { content: "Neat! Which mortar?" })).json?.data?.isEdited === true);
  t.check("post author deletes it (204)", (await call("DELETE", `/comments/${comment.commentId}`, author.token)).status === 204);

  t.section("Ownership and soft delete");
  t.check("stranger cannot edit the post (403)", (await call("PUT", `/posts/${post}`, stranger.token, { content: "hacked" })).status === 403);
  t.check("stranger cannot delete the post (403)", (await call("DELETE", `/posts/${post}`, stranger.token)).status === 403);
  t.check("author edits (shows edited)", (await call("PUT", `/posts/${post}`, author.token, { content: "Edited" })).status === 200
    && (await call("GET", `/posts/${post}`, reader.token)).json?.data?.isEdited === true);
  t.check("author deletes (200)", (await call("DELETE", `/posts/${post}`, author.token)).status === 200);
  t.check("deleted post is gone (404 everywhere)", (await call("GET", `/posts/${post}`, reader.token)).status === 404
    && (await call("POST", `/posts/${post}/like`, reader.token)).status === 404
    && !(await feed(reader, "all")).some(p => p.id === post));

  return t.finish();
}
