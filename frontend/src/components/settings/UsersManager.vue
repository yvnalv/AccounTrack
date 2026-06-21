<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { Plus, UserCircle } from 'lucide-vue-next'
import { usersApi } from '@/lib/users'
import { rolesApi } from '@/lib/roles'
import { companyApi } from '@/lib/company'
import type { User } from '@/types/users'
import type { Role } from '@/types/roles'
import type { Company } from '@/types/company'
import AppButton from '@/components/ui/AppButton.vue'
import AppInput from '@/components/ui/AppInput.vue'
import AppModal from '@/components/ui/AppModal.vue'
import FormField from '@/components/ui/FormField.vue'
import StatusBadge from '@/components/ui/StatusBadge.vue'

const { t } = useI18n()

const users = ref<User[]>([])
const roles = ref<Role[]>([])
const companies = ref<Company[]>([])
const loading = ref(true)
const modalOpen = ref(false)
const saving = ref(false)
const error = ref('')

const editing = ref<User | null>(null)
const form = reactive({
  email: '',
  password: '',
  fullName: '',
  roleIds: new Set<string>(),
  companyIds: new Set<string>(),
})

const roleName = (id: string) => roles.value.find((r) => r.id === id)?.name ?? '—'

async function load() {
  loading.value = true
  try {
    const [u, r, c] = await Promise.all([usersApi.list(), rolesApi.list(), companyApi.list()])
    users.value = u
    roles.value = r
    companies.value = c
  } finally {
    loading.value = false
  }
}
onMounted(load)

function openNew() {
  editing.value = null
  error.value = ''
  Object.assign(form, {
    email: '',
    password: '',
    fullName: '',
    roleIds: new Set<string>(),
    companyIds: new Set(companies.value.map((c) => c.id)), // default: all companies
  })
  modalOpen.value = true
}

function openEdit(user: User) {
  editing.value = user
  error.value = ''
  Object.assign(form, {
    email: user.email,
    password: '',
    fullName: user.fullName,
    roleIds: new Set(user.roleIds),
    companyIds: new Set(user.companyIds),
  })
  modalOpen.value = true
}

function toggle(set: Set<string>, id: string) {
  if (set.has(id)) set.delete(id)
  else set.add(id)
}

const canSave = computed(() => {
  if (!form.fullName.trim()) return false
  if (editing.value) return true
  return !!form.email.trim() && form.password.length >= 8
})

async function save() {
  if (!canSave.value) return
  saving.value = true
  error.value = ''
  try {
    if (editing.value) {
      await usersApi.update(editing.value.id, {
        fullName: form.fullName.trim(),
        roleIds: [...form.roleIds],
        companyIds: [...form.companyIds],
      })
    } else {
      await usersApi.create({
        email: form.email.trim(),
        password: form.password,
        fullName: form.fullName.trim(),
        roleIds: [...form.roleIds],
        companyIds: [...form.companyIds],
      })
    }
    modalOpen.value = false
    await load()
  } catch (e) {
    error.value = serverMessage(e) ?? t('settings.users.saveFailed')
  } finally {
    saving.value = false
  }
}

async function toggleActive(user: User) {
  try {
    await usersApi.setActive(user.id, !user.isActive)
    await load()
  } catch {
    /* surfaced on next load */
  }
}

function serverMessage(e: unknown): string | undefined {
  return (e as { response?: { data?: { message?: string } } })?.response?.data?.message
}
</script>

<template>
  <div class="space-y-4">
    <div class="flex items-center justify-between">
      <p class="text-sm text-text-muted">{{ t('settings.users.subtitle') }}</p>
      <AppButton @click="openNew"><Plus :size="16" /> {{ t('settings.users.new') }}</AppButton>
    </div>

    <div v-if="loading" class="text-sm text-text-muted">{{ t('common.loading') }}</div>
    <div v-else class="overflow-hidden rounded-lg border border-border">
      <table class="w-full text-sm">
        <thead class="bg-surface-2 text-left text-xs text-text-muted">
          <tr>
            <th class="px-3 py-2 font-medium">{{ t('settings.users.name') }}</th>
            <th class="px-3 py-2 font-medium">{{ t('settings.users.roles') }}</th>
            <th class="px-3 py-2 font-medium">{{ t('settings.users.status') }}</th>
            <th class="px-3 py-2"></th>
          </tr>
        </thead>
        <tbody>
          <tr v-if="!users.length"><td colspan="4" class="px-3 py-4 text-center text-text-muted">{{ t('settings.users.empty') }}</td></tr>
          <tr v-for="u in users" :key="u.id" class="border-t border-border" :class="{ 'opacity-60': !u.isActive }">
            <td class="px-3 py-2">
              <button class="flex items-center gap-2 text-left hover:underline" @click="openEdit(u)">
                <UserCircle :size="16" class="text-text-muted" />
                <span>
                  <span class="block font-medium text-text">{{ u.fullName }}</span>
                  <span class="block text-xs text-text-muted">{{ u.email }}</span>
                </span>
              </button>
            </td>
            <td class="px-3 py-2">
              <div class="flex flex-wrap gap-1">
                <StatusBadge v-for="rid in u.roleIds" :key="rid" tone="neutral" :label="roleName(rid)" />
                <span v-if="!u.roleIds.length" class="text-text-muted">—</span>
              </div>
            </td>
            <td class="px-3 py-2">
              <StatusBadge
                :tone="u.isActive ? 'positive' : 'neutral'"
                :label="u.isActive ? t('settings.users.active') : t('settings.users.inactive')"
              />
            </td>
            <td class="px-3 py-2 text-right">
              <button class="text-xs font-medium text-text-muted hover:underline" @click="toggleActive(u)">
                {{ u.isActive ? t('settings.users.deactivate') : t('settings.users.activate') }}
              </button>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <AppModal v-model="modalOpen" :title="editing ? editing.fullName : t('settings.users.new')">
      <div class="space-y-4">
        <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <FormField :label="t('settings.users.email')" required>
            <AppInput v-model="form.email" type="email" :disabled="!!editing" />
          </FormField>
          <FormField :label="t('settings.users.fullName')" required>
            <AppInput v-model="form.fullName" />
          </FormField>
        </div>
        <FormField v-if="!editing" :label="t('settings.users.password')" required>
          <input v-model="form.password" type="password" autocomplete="new-password" class="field-input" />
          <p class="mt-1 text-xs text-text-muted">{{ t('settings.users.passwordHint') }}</p>
        </FormField>

        <div>
          <p class="mb-1.5 text-sm font-medium text-text">{{ t('settings.users.roles') }}</p>
          <div class="grid grid-cols-1 gap-1.5 sm:grid-cols-2">
            <label v-for="r in roles" :key="r.id" class="flex items-center gap-2 text-sm text-text">
              <input type="checkbox" class="h-4 w-4 accent-accent" :checked="form.roleIds.has(r.id)" @change="toggle(form.roleIds, r.id)" />
              {{ r.name }}
            </label>
          </div>
        </div>

        <div v-if="companies.length > 1">
          <p class="mb-1.5 text-sm font-medium text-text">{{ t('settings.users.companies') }}</p>
          <div class="grid grid-cols-1 gap-1.5 sm:grid-cols-2">
            <label v-for="c in companies" :key="c.id" class="flex items-center gap-2 text-sm text-text">
              <input type="checkbox" class="h-4 w-4 accent-accent" :checked="form.companyIds.has(c.id)" @change="toggle(form.companyIds, c.id)" />
              {{ c.code }} — {{ c.name }}
            </label>
          </div>
        </div>

        <p v-if="error" class="text-sm text-negative">{{ error }}</p>
      </div>
      <template #footer>
        <AppButton variant="ghost" @click="modalOpen = false">{{ t('masterData.cancel') }}</AppButton>
        <AppButton :disabled="saving || !canSave" @click="save">
          {{ saving ? t('settings.users.saving') : t('settings.users.save') }}
        </AppButton>
      </template>
    </AppModal>
  </div>
</template>
