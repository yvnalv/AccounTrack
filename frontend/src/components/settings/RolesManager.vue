<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { Plus, Shield, Trash2 } from 'lucide-vue-next'
import { rolesApi } from '@/lib/roles'
import type { Permission, Role } from '@/types/roles'
import AppButton from '@/components/ui/AppButton.vue'
import AppInput from '@/components/ui/AppInput.vue'
import AppModal from '@/components/ui/AppModal.vue'
import FormField from '@/components/ui/FormField.vue'
import StatusBadge from '@/components/ui/StatusBadge.vue'

const { t } = useI18n()

const roles = ref<Role[]>([])
const permissions = ref<Permission[]>([])
const loading = ref(true)
const modalOpen = ref(false)
const saving = ref(false)
const error = ref('')

const editing = ref<Role | null>(null)
const form = reactive({ name: '', description: '', permissions: new Set<string>() })

const isAdmin = computed(() => editing.value?.isAdministrator ?? false)
const isSystem = computed(() => editing.value?.isSystem ?? false)

// Permissions grouped by module, in a stable module order.
const groups = computed(() => {
  const byModule = new Map<string, Permission[]>()
  for (const p of permissions.value) {
    if (!byModule.has(p.module)) byModule.set(p.module, [])
    byModule.get(p.module)!.push(p)
  }
  return [...byModule.entries()].map(([module, perms]) => ({ module, perms }))
})

async function load() {
  loading.value = true
  try {
    const [r, p] = await Promise.all([rolesApi.list(), rolesApi.permissions()])
    roles.value = r
    permissions.value = p
  } finally {
    loading.value = false
  }
}
onMounted(load)

function openNew() {
  editing.value = null
  error.value = ''
  Object.assign(form, { name: '', description: '', permissions: new Set<string>() })
  modalOpen.value = true
}

function openEdit(role: Role) {
  editing.value = role
  error.value = ''
  Object.assign(form, {
    name: role.name,
    description: role.description ?? '',
    permissions: new Set(role.permissions),
  })
  modalOpen.value = true
}

function toggle(code: string) {
  if (isAdmin.value) return
  if (form.permissions.has(code)) form.permissions.delete(code)
  else form.permissions.add(code)
}

function groupState(perms: Permission[]): 'all' | 'some' | 'none' {
  const on = perms.filter((p) => form.permissions.has(p.code)).length
  if (on === 0) return 'none'
  return on === perms.length ? 'all' : 'some'
}

function toggleGroup(perms: Permission[]) {
  if (isAdmin.value) return
  const all = groupState(perms) === 'all'
  for (const p of perms) {
    if (all) form.permissions.delete(p.code)
    else form.permissions.add(p.code)
  }
}

const canSave = computed(() => !!form.name.trim() && !isAdmin.value)

async function save() {
  if (!canSave.value) return
  saving.value = true
  error.value = ''
  const body = {
    name: form.name.trim(),
    description: form.description.trim() || null,
    permissions: [...form.permissions],
  }
  try {
    if (editing.value) await rolesApi.update(editing.value.id, body)
    else await rolesApi.create(body)
    modalOpen.value = false
    await load()
  } catch (e) {
    error.value = serverMessage(e) ?? t('settings.roles.saveFailed')
  } finally {
    saving.value = false
  }
}

async function remove() {
  if (!editing.value || editing.value.isSystem) return
  error.value = ''
  try {
    await rolesApi.remove(editing.value.id)
    modalOpen.value = false
    await load()
  } catch (e) {
    error.value = serverMessage(e) ?? t('settings.roles.deleteFailed')
  }
}

function serverMessage(e: unknown): string | undefined {
  return (e as { response?: { data?: { message?: string } } })?.response?.data?.message
}

function roleLabel(role: Role): string {
  // System role names map to friendly labels; custom roles show their own name.
  const key = `settings.roles.names.${role.name}`
  const translated = t(key)
  return translated === key ? role.name : translated
}
</script>

<template>
  <div class="space-y-4">
    <div class="flex items-center justify-between">
      <p class="text-sm text-text-muted">{{ t('settings.roles.subtitle') }}</p>
      <AppButton @click="openNew"><Plus :size="16" /> {{ t('settings.roles.new') }}</AppButton>
    </div>

    <div v-if="loading" class="text-sm text-text-muted">{{ t('common.loading') }}</div>
    <div v-else class="overflow-hidden rounded-lg border border-border">
      <table class="w-full text-sm">
        <thead class="bg-surface-2 text-left text-xs text-text-muted">
          <tr>
            <th class="px-3 py-2 font-medium">{{ t('settings.roles.name') }}</th>
            <th class="px-3 py-2 font-medium">{{ t('settings.roles.description') }}</th>
            <th class="px-3 py-2 text-right font-medium">{{ t('settings.roles.users') }}</th>
            <th class="px-3 py-2 text-right font-medium">{{ t('settings.roles.perms') }}</th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="role in roles"
            :key="role.id"
            class="cursor-pointer border-t border-border hover:bg-surface-2"
            @click="openEdit(role)"
          >
            <td class="px-3 py-2">
              <div class="flex items-center gap-2">
                <Shield :size="15" class="text-text-muted" />
                <span class="font-medium text-text">{{ roleLabel(role) }}</span>
                <StatusBadge v-if="role.isSystem" tone="neutral" :label="t('settings.roles.system')" />
              </div>
            </td>
            <td class="px-3 py-2 text-text-muted">{{ role.description || '—' }}</td>
            <td class="px-3 py-2 text-right tnum text-text-muted">{{ role.userCount }}</td>
            <td class="px-3 py-2 text-right tnum text-text-muted">
              {{ role.isAdministrator ? t('settings.roles.all') : role.permissions.length }}
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <AppModal v-model="modalOpen" size="lg" :title="editing ? roleLabel(editing) : t('settings.roles.new')">
      <div class="space-y-4">
        <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <FormField :label="t('settings.roles.name')" required>
            <AppInput v-model="form.name" :disabled="isSystem" />
          </FormField>
          <FormField :label="t('settings.roles.description')">
            <AppInput v-model="form.description" :disabled="isSystem" />
          </FormField>
        </div>

        <div v-if="isAdmin" class="rounded-card border border-border bg-surface-2 px-4 py-3 text-sm text-text-muted">
          {{ t('settings.roles.adminLocked') }}
        </div>

        <div v-else class="space-y-3">
          <p class="text-sm font-medium text-text">{{ t('settings.roles.permissions') }}</p>
          <div v-for="g in groups" :key="g.module" class="rounded-lg border border-border">
            <button
              type="button"
              class="flex w-full items-center justify-between bg-surface-2 px-3 py-2 text-left"
              @click="toggleGroup(g.perms)"
            >
              <span class="text-xs font-semibold uppercase tracking-wide text-text-muted">{{ g.module }}</span>
              <input
                type="checkbox"
                class="h-4 w-4 accent-accent"
                :checked="groupState(g.perms) === 'all'"
                :indeterminate="groupState(g.perms) === 'some'"
                @click.stop="toggleGroup(g.perms)"
              />
            </button>
            <div class="grid grid-cols-1 gap-x-4 gap-y-1.5 p-3 sm:grid-cols-2">
              <label
                v-for="p in g.perms"
                :key="p.code"
                class="flex items-center gap-2 text-sm text-text"
              >
                <input
                  type="checkbox"
                  class="h-4 w-4 accent-accent"
                  :checked="form.permissions.has(p.code)"
                  @change="toggle(p.code)"
                />
                {{ p.name }}
              </label>
            </div>
          </div>
        </div>

        <p v-if="error" class="text-sm text-negative">{{ error }}</p>
      </div>
      <template #footer>
        <AppButton
          v-if="editing && !editing.isSystem"
          variant="danger"
          class="mr-auto"
          @click="remove"
        >
          <Trash2 :size="15" /> {{ t('settings.roles.delete') }}
        </AppButton>
        <AppButton variant="ghost" @click="modalOpen = false">{{ t('masterData.cancel') }}</AppButton>
        <AppButton v-if="!isAdmin" :disabled="saving || !canSave" @click="save">
          {{ saving ? t('settings.roles.saving') : t('settings.roles.save') }}
        </AppButton>
      </template>
    </AppModal>
  </div>
</template>
